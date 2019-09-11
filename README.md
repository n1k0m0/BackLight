# BackLight
BackLight for TV (ESP8266 / Windows C#):
* BackLight (C# client)
* ESPServer (arduino-based server for LED strip)

![](Misc/BackLight.png)

This is just a hobby project I built myself for my living room. It is an arduino-based (ESP8266) backlight for my TV. Communication between client and server is done via UDP over wireless LAN. I do not take any responsibility if you damage you, yourself, your TV, or anything else by using my code, schematics, etc. Nevertheless, I hope you can use my code and ideas for yourself :-). Have fun playing with LED pixels...! 

Sample Videos of the BackLight:
* [BackLight Test1.mpg](https://github.com/n1k0m0/BackLight/raw/master/Demo%20Videos/BackLight%20Test1.mpg)
* [BackLight Test2.mpg](https://github.com/n1k0m0/BackLight/raw/master/Demo%20Videos/BackLight%20Test2.mpg)

Hardware:
* TV: The setup (hardware + software) is created for a full-hd (1080P) tv with 65"
* Micro controller: ESP8266 ESP-12E CH340G Wireless WIFI Internet Development Board (NodeMcu)
* Computer: I use a windows 10 pc (gigabyte brix, attached to my TV via HDMI)
* Led strip: WS2812B (300 LEDs)
* Power supply: DC 5V-24V 2A-80A

Software:
* Windows (client): Microsoft Visual Studio / C# .net 7.6.2; see BackLight folder of this project
* ESP8266 (server): Arduino; see ESPServer folder of this project

Of course, you can use my source code and adapt it to your needs. If you have any questions, don't hesitate writing me :-)

You have to change the definitions in Constants.cs as well as adapt the ESPServer.ino to your needs. In the ESPServer.ino you have to define your ssid and your wpa2-key. Also, you can change the listen port here. In the Constants.cs you have to adapt the ip address as well as the udp port accordingly.

Schematics:

The following figure shows the main setup I built for my tv. First, I only created a single strip going from the left downer corner to the top, then to the right, down, and back to the left. Since the LEDs at the end of the strip did not get enough power, I created a "circle" by soldering grounds and VCCs together. This allows my setup to light up the LEDs by 75%. I would suggest to also create two wires (VCC and ground) to the upper right corner, as shown in the schematics. And don't create a circle of the BUS wire (depicted as green wire in the figure).

![](Misc/schematics.png)

I have 86 LEDs on the top and 86 LEDs on the bottom of my screen.
I have 48 LEDs on the left and 48 LEDs on the right side of my screen.
This is a total of 82 * 2 + 48 * 2 = 260 LEDs for the complete screen.

Basic idea:

The basic idea of my setup is the following: The windows pc's software ("client") continuesly takes screenshots of the current screen at a rate of 10Hz (every 100ms -- this is fast enough). Each screenshot is analyzed by the program. For each side of the tv screen, "small rectangles of pixels" are analyzed. The program computes the "average color" of the particular rectangle of the screen. Then, it sends a UDP packet containing all color information for the LED strip to the NodeMCU. The NodeMCU takes these color informations and sends these to the strip. Since I had some problems with "false colors", I implemented a checksum (Fletcher-16) over the complete color array. After that, the server is able to check, if it received correct colors by comparing the received checksum with its own computed checksum. If the sums do not match, the packet is discarded. This happens seldomly, and can not be seen by the viewer. But after implementing the checksums, the "false colors" disappeared. 

The server is able to receive LED colors for each individual LED. But in my client, I send for each two consecutive LEDs the same color.

The client runs hidden in the background, but shows itself as small icon in the tray (right lower corner) of Windows. By right-click, you may stop the client as well as show and hide a debug window, which shows you the color information it currently sends to the server.

Network protocol:

The UDP packet starts with 1 byte for the brightness of all following pixels.
After that, the UDP packet may consist of an "arbritrary" number of LED informations (array), where a single LED information is:

```c
 struct LED_information
 {
  byte pixelLo;   // low byte of the offset of the pixel which should be changed
  byte pixelHi;   // high byte of the offset of the pixel which should be changed
  byte red;       // red part of pixel color
  byte green;     // green part of pixel color
  byte blue;      // blue part of pixel color
 }
```

The last two bytes of the packet have to be a "Fletcher-16 checksum" over the brightness byte, the previous array of LED informations, and two zero bytes (0x00). See https://en.wikipedia.org/wiki/Fletcher%27s_checksum#Fletcher-16 for details on that checksum.
 

