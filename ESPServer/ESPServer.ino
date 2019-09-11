/*                              
   Copyright 2019, Nils Kopal, nils<at>kopaldev.de

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*/

#include <Adafruit_NeoPixel.h>
#include <ESP8266WiFi.h>
#include <WiFiUDP.h>
#include <ESP8266mDNS.h>
 
#define PIXELS_PIN D5
#define LED_PIN 2
#define NUM_PIXELS 268
#define SERVER_PORT 1234

//Wifi settings
const char* ssid = "wlan ssid;
const char* password = "wlan password";
const char* hostname = "Pixelserver";

//Pixel controller
Adafruit_NeoPixel pixels = Adafruit_NeoPixel(NUM_PIXELS, PIXELS_PIN, NEO_GRB + NEO_KHZ800);

WiFiUDP server;

// 1 byte is brightness
// 5 byte per pixel
// 2 byte checksum
char data[1 + 48 * 5 + 86 * 5 + 48 * 5 + 86 * 5 + 2] = {};

void setup() 
{
  system_update_cpu_freq(160);  
  //Serial.begin(115200);  
  
  pinMode(LED_PIN, OUTPUT);

  pixels.begin();
  pixels.setBrightness(125);
  pixels.show();
  
  // inital connect
  WiFi.hostname(hostname);
  WiFi.mode(WIFI_STA);  
  WiFi.begin(ssid, password);  
  
  WiFiStart();
}

void WiFiStart()
{
  //turn led off
  digitalWrite(LED_PIN, HIGH);  
  while (WiFi.status() != WL_CONNECTED) 
  {
    delay(500);
    //turn led on
    digitalWrite(LED_PIN, LOW);     
    delay(100);
    //turn led off
    digitalWrite(LED_PIN, HIGH);
  }

  //turn led on
  digitalWrite(LED_PIN, LOW);     
  
  MDNS.begin(hostname);
  server.begin(1234);
}

// Main loop of the program
void loop() 
{
    delay(25);
    // check if WLAN is connected
    if (WiFi.status() != WL_CONNECTED)
    {
      WiFiStart();
    }

    int packetsize = server.parsePacket();    
    if (packetsize)
    {   
      //turn led off
      digitalWrite(LED_PIN, HIGH);
          
      int length = server.read(data, packetsize);
      if(length == 0)
      {
        return;
      }
      if(!checkChecksum(data,length))
      {
        //Serial.printf("Checksum not ok\r\n");
        return;  
      } 
      //Serial.printf("Checksum ok\r\n");     
      
      server.stop();      

      int brightness = (int)data[0];
      //Serial.printf("Brightness is %d\r\n",brightness);
      pixels.setBrightness(brightness);
      
      for(int i=1;i<packetsize - 2;i+=5)
      {        
        int pixelLo = (int)data[i];
        int pixelHi = (int)data[i+1];
        int r = (int)data[i+2];
        int g = (int)data[i+3];
        int b = (int)data[i+4];
        int pixel = pixelHi * 256 + pixelLo;           
        //Serial.printf("Coloring pixel %d (lo=%d, hi=%d) in %d %d %d\r\n",pixel, pixelLo, pixelHi, r,g,b);
        pixels.setPixelColor(pixel,r,g,b);        
      }
      pixels.show();
      
      server.begin(SERVER_PORT); 
               
      //turn led on
      digitalWrite(LED_PIN, LOW);
    }     
}

// Check Fletcher-16 checksum
bool checkChecksum(char data[], int length)
{
  char checksum[2];
  checksum[0] = data[length-2];
  checksum[1] = data[length-1];
  data[length-2] = 0;
  data[length-1] = 0;
    
  char sum1 = 0;
  char sum2 = 0;
  for (int i = 0; i < length; i++)
  {
      sum1 = (char)((sum1 + data[i]) % 255);
      sum2 = (char)((sum2 + sum1) % 255);
  }  
  return sum1 == checksum[0] && sum2 == checksum[1]; 
}
