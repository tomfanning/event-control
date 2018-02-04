/*
  Blink
  Turns on an LED on for one second, then off for one second, repeatedly.

  Most Arduinos have an on-board LED you can control. On the UNO, MEGA and ZERO 
  it is attached to digital pin 13, on MKR1000 on pin 6. LED_BUILTIN is set to
  the correct LED pin independent of which board is used.
  If you want to know what pin the on-board LED is connected to on your Arduino model, check
  the Technical Specs of your board  at https://www.arduino.cc/en/Main/Products
  
  This example code is in the public domain.

  modified 8 May 2014
  by Scott Fitzgerald
  
  modified 2 Sep 2016
  by Arturo Guadalupi
  
  modified 8 Sep 2016
  by Colby Newman
*/


// the setup function runs once when you press reset or power the board
void setup() {
  // initialize digital pin LED_BUILTIN as an output.
  pinMode(LED_BUILTIN, OUTPUT);
  pinMode(10, OUTPUT);
  //pinMode(6, OUTPUT);
  Serial.begin(38400);
}

int ditlen = 50;
int dahlen = ditlen*3;
int gaplen = ditlen;
int pin=6;
int freq = 700;

void dit(int num){
  for (int i=0;i<num;i++){
    tone(pin,freq,ditlen);
    delay(ditlen);
    delay(ditlen);
  }
}

void dah(int num){
  for (int i=0;i<num;i++){
    tone(pin,freq,dahlen);
    delay(dahlen);
    delay(ditlen);
  }
}

void gap() {
  delay(dahlen);
}

int incomingByte;

// the loop function runs over and over again forever
void loop() {
  Serial.print(" keyed");
  digitalWrite(LED_BUILTIN, HIGH);   // turn the LED on (HIGH is the voltage level)
  digitalWrite(10, HIGH);
  
  dah(2);
  gap();
  dah(5);
  gap();
  dit(1);dah(1);dit(2);
  gap();
  dah(1);
  gap();
  dit(1);

  delay(1000);                       // wait for a second
  
  Serial.println(" unkeyed");
  digitalWrite(LED_BUILTIN, LOW);
  digitalWrite(10, LOW);// turn the LED off by making the voltage LOW

  unsigned long start = millis();
  while (millis() < start + 15000) {
    if (Serial.available() > 0) {
    
      // read the incoming byte:
      incomingByte = Serial.read();
    
      // say what you got:
      Serial.print((char)incomingByte);
    }
  }
}
