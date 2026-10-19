// SZH-SSBH-011: VCC -> Mega 5V, GND -> GND, AO -> A0. DO unused.
// The Mega talks to the Pi over USB, not through a 5V GPIO signal.
const int LIGHT_PIN = A0;

void setup() {
  Serial.begin(115200);
}

void loop() {
  Serial.println(analogRead(LIGHT_PIN));
  delay(200);
}
