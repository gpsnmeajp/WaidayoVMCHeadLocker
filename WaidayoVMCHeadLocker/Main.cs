/*
MIT License

Copyright (c) 2020 gpsnmeajp

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/
using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rug.Osc;
using Newtonsoft.Json;
/*
 * InputJson : 入力ポートと入力名
 * OutputJson : 出力ポートと入力名
 * Filter Json: 振り分け法則(入力名-フィルタ-出力名)
 */

namespace WaidayoVMCHeadLocker
{
    enum FROM{ 
        VMC,
        Waidayo
    }

    class Main
    {
        InputOSC OSCfromWaidayo;
        InputOSC OSCfromVMC;
        OutputOSC OSCtoVMC;
        OutputOSC OSCtoEVMC4U;

        float tx = 0;
        float ty = 0;
        float tz = 0;

        int calib = 0;

        public void Process()
        {
            Console.WriteLine("### WaidayoVMCHeadLocker v0.01");
            StartServer();
            Console.WriteLine("Press ENTER key to stop server");
            Console.ReadLine();
            StopServer();

            Console.WriteLine("Press ENTER key to close window");
            Console.ReadLine();
        }

        private void StartServer()
        {
            OSCfromWaidayo = new InputOSC("fromWaidayo", 39530, (name, bundle) => {
                //Waidayoからバンドルが来ました
                //基本全部VMCに流します
                //Headボーンの情報は取り出しておきます
                OnBundle(FROM.Waidayo, bundle);
                OSCtoVMC.Send(ReBundle(bundle));
            }, (name, message) => {
                OnMessage(FROM.Waidayo, message);
                OSCtoVMC.Send(message);
            });
            OSCtoVMC = new OutputOSC("127.0.0.1", 39540);

            OSCfromVMC = new InputOSC("fromVMC",39539,(name,bundle)=> {
                //VMCからバンドルが来ました
                //全部EVMC4Uに流します
                //デバイス情報は取り出しておきます
                //キャリブ状態は取り出しておきます

                //仮想トラッカーを生成して流し込みます

                OnBundle(FROM.VMC, bundle);
                OSCtoEVMC4U.Send(ReBundle(bundle));
            }, (name,message)=> {
                OnMessage(FROM.VMC, message);
                OSCtoEVMC4U.Send(message);
            });
            OSCtoEVMC4U = new OutputOSC("127.0.0.1", 39550);
        }
        private void StopServer()
        {
            OSCfromVMC.Dispose();
            OSCfromWaidayo.Dispose();
            OSCtoEVMC4U.Dispose();
            OSCtoVMC.Dispose();
        }

        private OscBundle ReBundle(OscBundle bundle)
        {
            List<OscMessage> messages = new List<OscMessage>();
            for (int i = 0; i < bundle.Count; i++)
            {
                switch (bundle[i])
                {
                    //Messageを受信した
                    case OscMessage msg:
                        messages.Add(msg);
                        break;
                    default:
                        //Do noting
                        break;
                }
            }
//            messages.Add(new OscMessage("/padding"));
            return new OscBundle(0, messages.ToArray());
        }

        private void OnBundle(FROM from,OscBundle bundle)
        {
            for (int i = 0; i < bundle.Count; i++)
            {
                switch (bundle[i])
                {
                    //Messageを受信した
                    case OscMessage msg:
                        OnMessage(from,msg);
                        break;
                    default:
                        //Do noting
                        break;
                }
            }
        }

        private void OnMessage(FROM from, OscMessage message)
        {
            //ばもきゃ
            if (from == FROM.VMC && message.Address == "/VMC/Ext/OK")
            {
                calib = (int)message[1];
                Console.WriteLine(message);
            }
            if (from == FROM.VMC && message.Address == "/VMC/Ext/Tra/Pos/Local" && (string)message[0] == "LHR-B38070EB")
            {
                if (calib == 1) {
                    tx = (float)message[1];
                    ty = (float)message[2];
                    tz = (float)message[3];
                }
                //Console.WriteLine(message);
            }
            //Waidayo
            if (from == FROM.Waidayo && message.Address == "/VMC/Ext/Bone/Pos" && (string)message[0] == "Head")
            {
                float qx = (float)message[4];
                float qy = (float)message[5];
                float qz = (float)message[6];
                float qw = (float)message[7];

                object[] arg = new object[]{ "HeadLockTracker",tx,ty,tz, -qx,qy, -qz,qw};
                OscMessage m = new OscMessage("/VMC/Ext/Tra/Pos", arg);
                OSCtoVMC.Send(m);
                Console.WriteLine(m);
            }
        }
    }
}
