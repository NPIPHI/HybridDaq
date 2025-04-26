using System;
using System.Threading.Tasks;
using System.IO.Ports;
using Windows.Storage;
using System.IO;
using Windows.System;
using Windows.UI.Xaml.Automation.Peers;
using System.Linq.Expressions;
using Windows.Gaming.Input.ForceFeedback;
using System.Text;

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
                        server.action = BallValveAction.Arm;
                        break;
                    case "d":
                        server.action = BallValveAction.Disarm;
                        break;
                    case "o":
                        server.action = BallValveAction.Open;
                        break;
                    case "c":
                        server.action = BallValveAction.Close;
                        break;
                    case "f":
                        server.action = BallValveAction.Fire;
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
                SerialPort port = new SerialPort(address, 115200);
                StorageFile file = await DownloadsFolder.CreateFileAsync(DateTime.Now.ToString("yyyy.MM.dd.HH.mm.ss") + ".csv");
                Console.WriteLine($"Logging to {file.Path}");
                BufferedStream csvLog = new BufferedStream(await file.OpenStreamForWriteAsync());

                port.Open();
                Console.WriteLine("Port opened");
                byte[] header = Encoding.UTF8.GetBytes("p1,p2,p3,p4\n");
                csvLog.Write(header,0,header.Length);
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
                    if (server.action == BallValveAction.Arm)
                    {
                        port.Write("a");
                        server.action = BallValveAction.None;
                    }
                    if (server.action == BallValveAction.Disarm)
                    {
                        port.Write("d");
                        server.action = BallValveAction.None;
                    }
                    if (server.action == BallValveAction.Fire)
                    {
                        port.Write("f");
                        server.action = BallValveAction.None;
                    }

                    string line = port.ReadLine();
                    string[] split = line.Split(new char[] { '\t', '\r', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (split.Length != 13)
                    {
                        continue;
                    }
                    try
                    {
                        int millis = int.Parse(split[0]);
                        float pressure1 = float.Parse(split[1]);
                        float pressure2 = float.Parse(split[2]);
                        float pressure3 = float.Parse(split[3]);
                        float pressure4 = float.Parse(split[4]);
                        float pyro_a_volt = float.Parse(split[5]);
                        float pyro_b_volt = float.Parse(split[6]);
                        float pyro_in_0_volt = float.Parse(split[7]);
                        float pyro_in_1_volt = float.Parse(split[8]);
                        int armed = int.Parse(split[9]);
                        int ballvalve_open = int.Parse(split[10]);
                        int ballvalve_engaged = int.Parse(split[11]);
                        float force_kg = float.Parse(split[12]);
                        byte[] log_line = Encoding.UTF8.GetBytes(String.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12}\n", 
                            millis,
                            pressure1, pressure2, pressure3, pressure4,
                            pyro_a_volt, pyro_b_volt, pyro_in_0_volt, pyro_in_1_volt,
                            armed, ballvalve_open, ballvalve_engaged, force_kg));
                        csvLog.Write(log_line, 0, log_line.Length);

                        Console.Write("\r{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11}", 
                            pressure1, pressure2, pressure3, pressure4,
                            pyro_a_volt, pyro_b_volt, pyro_in_0_volt, pyro_in_1_volt,
                            armed, ballvalve_open, ballvalve_engaged, force_kg);


                        //avg1 = avg1 * 0.9 + (float)pressure1 * 0.1;
                        avg2 = avg2 * 0.95 + (float)pressure2 * 0.05;
                        avg3 = avg3 * 0.95 + (float)pressure3 * 0.05;
                        //server.force = 100;
                        //server.force = force;
                        server.pressure1 = (float)avg2;
                        server.pressure2 = (float)avg3;
                        server.ball_valve_open = ballvalve_open == 1;
                        server.is_armed = armed == 1;
                        server.ball_valve_engaged = ballvalve_engaged == 1;
                        server.pyro0 = pyro_a_volt;
                        server.pyro1 = pyro_b_volt;
                        //server.ballValveOpen = (is_open == 1);
                        //server.det_voltage = det_voltage;
                        //server.is_auto_armed = (is_auto_armed == 1);
                        //csvLog.WriteLine("{0},{1},{2},{3},{4},{5},{6},{7},{8}", millis, force, pressure1, pressure2, pressure3, det_voltage, is_open, is_auto_armed, ematch_detected_time);
                        //Console.Write("\r{0},{1},{2},{3},{4},{5},{6},{7}...............", millis, force, pressure1, pressure2, pressure3, det_voltage, is_open, is_auto_armed);
                    }
                    catch (System.FormatException e)
                    {
                        Console.WriteLine(e.ToString());
                    }

                }

                port.Close();
                csvLog.Flush();
                csvLog.Close();
            } catch(System.AggregateException e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }
}
