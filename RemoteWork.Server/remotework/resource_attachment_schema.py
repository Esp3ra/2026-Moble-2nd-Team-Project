"""Explicit member1 physical-schema check for member3 resource attachment ports.

No model import, registration, DDL, permissions or global verification cache.
Callers decide when an absent optional storage is allowed. Partial/bad storage
always fails closed. This check does not authorize resource access.
"""
import re

from sqlalchemy import inspect, text

from .common import AppError


TABLES = {
    'result_resource_uploads': (
        {'attachment_id': ('VARCHAR', 36, False), 'resource_type': ('VARCHAR', 16, False),
         'context_id': ('VARCHAR', 36, True)}, ['attachment_id'],
        {(('attachment_id',), 'result_shared_files', ('id',)),
         (('context_id',), 'chat_rooms', ('id',))}),
    'result_notice_files': (
        {'notice_id': ('INTEGER', None, False), 'attachment_id': ('VARCHAR', 36, False)},
        ['notice_id', 'attachment_id'],
        {(('notice_id',), 'notices', ('id',)), (('attachment_id',), 'result_shared_files', ('id',))}),
    'result_chat_files': (
        {'message_id': ('VARCHAR', 36, False), 'attachment_id': ('VARCHAR', 36, False)},
        ['message_id', 'attachment_id'],
        {(('message_id',), 'chat_messages', ('message_id',)),
         (('attachment_id',), 'result_shared_files', ('id',))}),
}


def _unavailable():
    raise AppError(503, 'RESOURCE_ATTACHMENT_SCHEMA_REQUIRED',
                   '공지·채팅 첨부 DB의 실제 구조·참조 조건을 확인해 주세요.')


def _expression(value):
    # Preserve AND/OR grouping; deleting all parentheses can hide a different
    # CHECK. MySQL adds redundant parentheses/charset introducers in SHOW CREATE.
    value = re.sub(r'[\s`]+', '', value.lower().replace('_utf8mb4', ''))
    for atom, token in (("hex(resource_type)=hex('notice')", 'A'),
                        ("hex(resource_type)=hex('chat')", 'C'),
                        ('context_idisnotnull', 'D'), ('context_idisnull', 'B')):
        value = value.replace(atom, token)
    tokens = re.findall(r'[ABCD()]|and|or', value)
    if ''.join(tokens) != value:
        return None
    offset = 0
    def factor():
        nonlocal offset
        token = tokens[offset]; offset += 1
        if token == '(':
            result = expression()
            if tokens[offset] != ')':
                raise ValueError()
            offset += 1
            return result
        if token not in 'ABCD' or len(token) != 1:
            raise ValueError()
        return token
    def conjunction():
        nonlocal offset
        result = factor()
        while offset < len(tokens) and tokens[offset] == 'and':
            offset += 1
            result = ('and', result, factor())
        return result
    def expression():
        nonlocal offset
        result = conjunction()
        while offset < len(tokens) and tokens[offset] == 'or':
            offset += 1
            result = ('or', result, conjunction())
        return result
    try:
        result = expression()
        return result if offset == len(tokens) else None
    except (ValueError, IndexError):
        return None


def require_resource_attachment_schema(db, *, optional=False):
    """Check this session's MySQL connection without committing or caching.

    False only means all three optional tables are absent. True means the
    reviewed columns/PK/FK/index/CHECK/engine/collation currently match 015.
    Existing 003/005/013 parent ownership, data and authorization remain intact.
    """
    connection = db.connection()
    if connection.dialect.name != 'mysql':
        _unavailable()
    inspector = inspect(connection)
    present = [inspector.has_table(name) for name in TABLES]
    if optional and not any(present):
        return False
    if not all(present):
        _unavailable()
    schema = inspector.default_schema_name
    for name, (expected, pk, wanted_fks) in TABLES.items():
        options = inspector.get_table_options(name)
        if (str(options.get('mysql_engine', '')).lower() != 'innodb'
                or options.get('mysql_collate') != 'utf8mb4_unicode_ci'):
            _unavailable()
        columns = {c['name']: c for c in inspector.get_columns(name)}
        if set(columns) != set(expected) or inspector.get_pk_constraint(name)['constrained_columns'] != pk:
            _unavailable()
        for field, (kind, length, nullable) in expected.items():
            actual = columns[field]
            if (type(actual['type']).__name__ != kind or actual['nullable'] != nullable
                    or actual.get('default') is not None
                    or actual.get('autoincrement') is True
                    or getattr(actual['type'], 'unsigned', False)
                    or length is not None and (actual['type'].length != length
                        or actual['type'].collation != 'utf8mb4_unicode_ci')):
                _unavailable()
        fks = inspector.get_foreign_keys(name)
        found = {(tuple(f['constrained_columns']), f['referred_table'], tuple(f['referred_columns'])) for f in fks}
        if (len(fks) != len(wanted_fks) or found != wanted_fks
                or any(f.get('referred_schema') not in (None, schema)
                    or any(str(f.get('options', {}).get(k, 'RESTRICT')).upper() not in ('RESTRICT', 'NO ACTION')
                           for k in ('ondelete', 'onupdate')) for f in fks)):
            _unavailable()
        # FK columns need a supporting left-prefix index. PK may supply it.
        indexes = [pk, *[i['column_names'] for i in inspector.get_indexes(name)]]
        if any(not any(tuple(i[:len(cols)]) == cols for i in indexes) for cols, _, _ in wanted_fks):
            _unavailable()
        checks = inspector.get_check_constraints(name)
        if name == 'result_resource_uploads':
            expected_check = "(HEX(resource_type)=HEX('notice') AND context_id IS NULL) OR (HEX(resource_type)=HEX('chat') AND context_id IS NOT NULL)"
            if (len(checks) != 1 or checks[0]['name'] != 'ck_resource_upload_context'
                    or _expression(checks[0]['sqltext']) != _expression(expected_check)):
                _unavailable()
            enforced = connection.scalar(text('SELECT ENFORCED FROM information_schema.TABLE_CONSTRAINTS '
                'WHERE CONSTRAINT_SCHEMA=:schema AND TABLE_NAME=:name AND CONSTRAINT_NAME=:constraint'),
                dict(schema=schema, name=name, constraint='ck_resource_upload_context'))
            if enforced != 'YES':
                _unavailable()
        elif checks:
            _unavailable()
    return True
