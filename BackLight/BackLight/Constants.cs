﻿/*                              
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

namespace BackLight
{
    public class Constants
    {
        //this field may be changed by the user using the context menu in the tray (show/hide ui)        
        public static bool DebugDraw = false;
        public static bool StaticColor = false;
        //The minimum R,G,B value for each pixel e.g. 0,0,0 used for all LEDs
        public static byte MinColorValue = 0;        
        public const int verticalOffset = 0;        
    }

    public enum Resolution
    {
        R_1080p,
        R_4K
    }
}
