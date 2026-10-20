param([string]$RuntimeConfig)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName Microsoft.CodeAnalysis.CSharp
$taskProject=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../RemoteWork.WinForms'))
$parseOptions=[Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::Default.WithLanguageVersion([Microsoft.CodeAnalysis.CSharp.LanguageVersion]::CSharp12)
$trees=[System.Collections.Generic.List[Microsoft.CodeAnalysis.SyntaxTree]]::new()
foreach($sourceFile in Get-ChildItem -LiteralPath $taskProject -Filter '*.cs' -File -Recurse){
 if($sourceFile.FullName -match '[\\/](obj|bin)[\\/]'){continue}
 $sourcePath=$sourceFile.FullName
 $trees.Add([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText($sourcePath),$parseOptions,$sourceFile.FullName))
}
$trees.Add([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'verify_client.cs')),$parseOptions,'verify_client.cs'))
$globalSource='global using System; global using System.Collections.Generic; global using System.IO; global using System.Linq; global using System.Net.Http; global using System.Threading; global using System.Threading.Tasks; global using System.Drawing; global using System.Windows.Forms; namespace RemoteWork.WinForms { internal static class ApplicationConfiguration { public static void Initialize() {} } }'
$trees.Add([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($globalSource,$parseOptions,'SdkBindings.cs'))
$references=[System.Collections.Generic.List[Microsoft.CodeAnalysis.MetadataReference]]::new()
foreach($referenceRoot in @('C:/Program Files/dotnet/packs/Microsoft.NETCore.App.Ref/8.0.29/ref/net8.0','C:/Program Files/dotnet/packs/Microsoft.WindowsDesktop.App.Ref/8.0.29/ref/net8.0')){
 foreach($referenceFile in Get-ChildItem -LiteralPath $referenceRoot -Filter '*.dll' -File){
 $references.Add([Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile($referenceFile.FullName))
 }
}
$options=[Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions]::new([Microsoft.CodeAnalysis.OutputKind]::DynamicallyLinkedLibrary).WithNullableContextOptions([Microsoft.CodeAnalysis.NullableContextOptions]::Enable)
$compilation=[Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create('ReviewsBundleCheck',$trees.ToArray(),$references.ToArray(),$options)
$memory=[IO.MemoryStream]::new()
$result=$compilation.Emit($memory)
$result.Diagnostics | Where-Object Severity -in @('Warning','Error') | ForEach-Object ToString
Write-Output ('Success='+$result.Success+'; Sources='+$trees.Count+'; Errors='+@($result.Diagnostics | Where-Object Severity -eq Error).Count+'; Warnings='+@($result.Diagnostics | Where-Object Severity -eq Warning).Count)
if(!$result.Success){$memory.Dispose();exit 1}
[void][System.Reflection.Assembly]::LoadFrom('C:/Program Files/dotnet/shared/Microsoft.WindowsDesktop.App/10.0.9/System.Windows.Forms.dll')
$assembly=[System.Reflection.Assembly]::Load($memory.ToArray())
$memory.Dispose()
$entry=$assembly.GetType('RemoteWork.WinForms.Features.Results.ResultsFunctionalCheck').GetMethod('Run')
try {
 $message=$entry.Invoke($null,@([IO.File]::ReadAllText($RuntimeConfig)))
 Write-Output $message
} catch {
 $errorValue=$_.Exception
 while($errorValue.InnerException){$errorValue=$errorValue.InnerException}
 Write-Output ('FAIL C#: '+$errorValue.GetType().Name+' '+$errorValue.Message)
 exit 1
}
