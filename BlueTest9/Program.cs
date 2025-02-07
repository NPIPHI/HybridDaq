using System;
using System.Threading.Tasks;
using System.IO.Ports;
using Windows.Storage;
using System.IO;
using Windows.System;
using Windows.UI.Xaml.Automation.Peers;
using System.Linq.Expressions;
using Windows.Gaming.Input.ForceFeedback;

// This example code shows how you could implement the required main function for a 
// Console UWP Application. You can replace all the code inside Main with your own custom code.

// You should also change the Alias value in the AppExecutionAlias Extension in the 
// Package.appxmanifest to a value that you define. To edit this file manually, right-click
// it in Solution Explorer and select View Code, or open it with the XML Editor.

namespace BlueTest9
{
    class Program {
        public static BLEServer server;
        public static bool should_quit;
        static void Main(string[] args)
        {
            Console.WriteLine("Enter COM port");
            string port_address = Console.ReadLine();
            server = new BLEServer();
            var started = server.start();
            Task.WaitAll(started);
            if (!started.Result)
            {
                Console.WriteLine("Server Setup Failed");
                return;
            }
            Console.WriteLine("Bluetooth Server Open");
            Task serialTask = Task.Run(()=>RunSerial(port_address));


            while (!should_quit)
            {
                string line = Console.ReadLine();
                switch (line)
                {
                    case "a":
                        server.action = BallValveAction.AutoArm;
                        break;
                    case "d":
                        server.action = BallValveAction.AutoDisarm;
                        break;
                    case "o":
                        server.action = BallValveAction.Open;
                        break;
                    case "c":
                        server.action = BallValveAction.Close;
                        break;
                    case "quit":
                        should_quit = true;
                        break;
                    default:
                        Console.WriteLine("unrecognized action {0}", line);
                        break;
                }
            }
            Task.WaitAll(serialTask);
        }
        static async Task RunSerial(string address)
        {
            Console.WriteLine("Running Serial");
            try
            {
                SerialPort port = new SerialPort(address, 38400);
                StorageFile file = await DownloadsFolder.CreateFileAsync(DateTime.Now.ToString("yyyy.MM.dd.HH.mm.ss") + ".csv");
                Console.WriteLine($"Logging to {file.Path}");
                StreamWriter csvLog = new StreamWriter(await file.OpenStreamForWriteAsync());
                port.Open();
                Console.WriteLine("Port opened");
                csvLog.WriteLine("millis,force,p1,p2,p3,dev_volt,is_open,is_auto_armed,ematch_detect_time");
                port.Write(" ");
                double avg1 = 0.0;
                double avg2 = 0.0;
                double avg3 = 0.0;
                
                while (!should_quit)
                {
                    if (server.action == BallValveAction.Close)
                    {
                        port.Write("c");
                        server.action = BallValveAction.None;
                    }
                    if (server.action == BallValveAction.Open)
                    {
                        port.Write("o");
                        server.action = BallValveAction.None;
                    }
                    if (server.action == BallValveAction.AutoArm)
                    {
                        port.Write("a");
                        server.action = BallValveAction.None;
                    }
                    if (server.action == BallValveAction.AutoDisarm)
                    {
                        port.Write("d");
                        server.action = BallValveAction.None;
                    }

                    string line = port.ReadLine();
                    string[] split = line.Split(new char[] { '\t', '\r', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (split.Length != 9)
                    {
                        continue;
                    }
                    try
                    {
                        int millis = int.Parse(split[0]);
                        float force = float.Parse(split[1]);
                        float pressure1 = float.Parse(split[2]);
                        float pressure2 = float.Parse(split[3]);
                        float pressure3 = float.Parse(split[4]);
                        float det_voltage = float.Parse(split[5]);
                        int is_open = int.Parse(split[6]);
                        int is_auto_armed = int.Parse(split[7]);
                        int ematch_detected_time = int.Parse(split[7]);
                        avg1 = avg1 * 0.9 + (float)pressure1 * 0.1;
                        avg2 = avg2 * 0.9 + (float)pressure2 * 0.1;
                        avg3 = avg3 * 0.9 + (float)pressure3 * 0.1;
                        server.force = 100;
                        server.force = force;
                        server.pressure1 = (float)avg1;
                        server.pressure2 = (float)avg2;
                        server.ballValveOpen = (is_open == 1);
                        server.det_voltage = det_voltage;
                        server.is_auto_armed = (is_auto_armed == 1);
                        csvLog.WriteLine("{0},{1},{2},{3},{4},{5},{6},{7},{8}", millis, force, pressure1, pressure2, pressure3, det_voltage, is_open, is_auto_armed, ematch_detected_time);
                        Console.Write("\r{0},{1},{2},{3},{4},{5},{6},{7}...............", millis, force, pressure1, pressure2, pressure3, det_voltage, is_open, is_auto_armed);
                    }
                    catch (System.FormatException e)
                    {
                        Console.WriteLine(e.ToString());
                    }

                }

                port.Close();
                csvLog.Close();
            } catch(System.AggregateException e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}
