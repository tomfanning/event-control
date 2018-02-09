void setup() {
  Serial.begin(9600);
}

bool primed = false;

int pktLen;
byte pkt[900];

void loop() {

  if (Serial.available()) {
    byte b = Serial.read();
    
    if (!primed && b == 0xc0) {
      primed = true;
    } else if (primed && b == 0x00) {
      // start of packet
      pktLen=2;
      pkt[0]=0xc0;
      pkt[1]=0x00;
    } else if (b == 0xc0) {
      pkt[pktLen] = b;
      processPacket(pkt, pktLen);
      primed = false;
    }
  }
}

void processPacket(byte pkt[], int pktLen) {
  
}

void test() {

  byte frame[] = {0xC0, 0x00, 
                  0xAA, 0xA2, 0xA4, 0xAC, 0xAA, 0xA2, 0x60, //   to: UQRVUQ  CBit=0 IsLast=0 Res=11
                  0x9A, 0x60, 0x98, 0xA8, 0x8A, 0x40, 0xEA, // from: M0LTE-5 CBit=1 IsLast=0 Res=11
                  0xAE, 0x92, 0x88, 0x8A, 0x62, 0x40, 0x62, // via1: WIDE1-1
                  0xAE, 0x92, 0x88, 0x8A, 0x64, 0x40, 0x65, // via2: WIDE2-2 IsLast=1
                  0x03, 0xF0, 
                  0x27, 0x77, 0x59, 0x44, 0x6C, 0x20, 0x1C, 0x5B, 0x2F, 0x3E, 0x0D, 
                  0xC0};

  int frameBytes = sizeof(frame);
  
  byte outputBuf[frameBytes + 7];

  processFrame(frame, outputBuf);
  
  for (int i=0;i<sizeof(outputBuf); i++) {
    printByte(outputBuf[i]);
  }

  while(true){}
  //                 dest                             source                                  digi 1                 digi 2                 digi 3          
  //          0  1    2  3  4  5  6  7  8              9 10 11 12 13 14 15                    16 17 18 19 20 21 22
  // expect: C0 00   AA A2 A4 AC AA A2 60             9A 60 98 A8 8A 40 EA                    AE 92 88 8A 62 40 62   AE 92 88 8A 64 40 64   9A 60 98 A8 8A 40 F3   03 F0    27 77 59 44 6C 20 1C 5B 2F 3E 0D   C0
  // actual: C0 00   AA A2 A4 AC AA A2 60             9A 60 98 A8 8A 40 EA                    AE 92 88 8A 62 40 62   AE 92 88 8A 64 40 64   9A 60 98 A8 8A 40 F3   03 F0    27 77 59 44 6C 20 1C 5B 2F 3E 0D   C0
  // actual: C0 00   AA A2 A4 AC AA A2 60             9A 60 98 A8 8A 40 EA                    AE 92 88 8A 62 40 62   AE 92 88 8A 64 40 64   9A 60 98 A8 8A 40 F3   03 F0    27 77 59 44 6C 20 1C 5B 2F 3E 0D   C0
}

// mycall M0LTE-9 with isLast = true
//                       M     0     L     T     E    spc   9+L
const byte mycall[] = {0x9A, 0x60, 0x98, 0xA8, 0x8A, 0x40, 0xF3};

void processFrame(byte frame[], byte outputBuf[]) {

  //Serial.println();
  
  /*
      header
      dest the same
      source the same
      start looking for delim / islast
      all digis the same apart from set previous last digi islast to false
      then append own call with islast = true
      info the same
      fend the same
   */
  int elements = sizeof(frame)/8;

  for (int i=0; i<16; i++) {
      // header, two callsigns (source and dest)
      //sendByte(frame[i]);
      outputBuf[i] = frame[i];
  }

  // digipeater fields
  int pos=16;
  while (true) {

    // send the callsign part as-is
    for (int i=0; i<6; i++) {
      //sendByte(frame[i+pos]);
      outputBuf[i+pos] = frame[i+pos];
    }

    // now for the ssid byte
    // bits numbered from the right, per fig 3.5 of http://www.tapr.org/pdf/AX25.2.2.pdf
    // bit 0 (rightmost) is the HDLC address extension bit, set to zero on all but the last octet in the address field, where it is set to one.

    byte ssidByte = frame[pos+6];
    
    if (bitRead(ssidByte,0)) {
      // this is the last digi
      // unset the isLastBit bit, send the SSID byte
      bitClear(ssidByte,0);
      //sendByte(ssidByte);
      outputBuf[pos+6] = ssidByte;
      pos += 7;
      break;
    } else {
      // send the SSID byte as-is
      //sendByte(ssidByte);
      outputBuf[pos+6] = ssidByte;
      // move on to the next address field
      pos += 7; 
    }
  }
  
  // add our own call with isLastBit set to true
  for (int i=0; i<7;i++) {
    //sendByte(mycall[i]);
    outputBuf[pos+i] = mycall[i];
  }

  // send the delimiter and everything else after it until c0

  while (true) {
    //sendByte(frame[pos]/*, pos < elements+7*/);
    outputBuf[pos+7] = frame[pos];
    if (frame[pos] == 0xc0) {
      return;
    } 
    pos++;
  } 
}

void printByte(byte b) {
  Serial.print(' ');
  if (b < 16) {Serial.print("0");}
  Serial.print(b, HEX);
}

