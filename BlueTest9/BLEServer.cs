using System;
using Windows.Foundation;
using Windows.Devices.Bluetooth;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace BlueTest9
{
    public enum BallValveAction
    {
        Open,
        Close,
        Arm,
        Disarm,
        Fire,
        None
    }
    public class BLEServer
    {
        private static readonly Guid serviceID = Guid.Parse("47da423a-5990-4b7a-b193-9b56c95926b1");
        private static readonly Guid daqID = Guid.Parse("55c39af6-6f39-4ba6-8d49-6066c30ca1e9");
        private static readonly Guid actuateID = Guid.Parse("07ecb91a-5534-44ca-8a97-942ef72e4e6b");
        private static readonly int OpenCode = 129035867;
        private static readonly int CloseCode = 2065891205;
        private static readonly int ArmCode = 671238032;
        private static readonly int DisarmCode = 301529212;
        private static readonly int FireCode = 913844192;
        private GattServiceProvider serviceProvider;
        private GattLocalCharacteristic DAQCharacteristic;
        private GattLocalCharacteristic actuateCharacteristic;
        public float force = 0;
        public float pressure1 = 0;
        public float pressure2 = 0;
        public bool ball_valve_open = false;
        public bool is_armed = false;
        public bool ball_valve_engaged = false;
        public BallValveAction action = BallValveAction.None;
        private static readonly GattLocalCharacteristicParameters ReadParams = new GattLocalCharacteristicParameters
        {
            CharacteristicProperties = GattCharacteristicProperties.Read | GattCharacteristicProperties.Notify,
            WriteProtectionLevel = GattProtectionLevel.Plain,
            UserDescription = "DAQ Characteristc",
        };

        private static readonly GattLocalCharacteristicParameters ActuateParams = new GattLocalCharacteristicParameters
        {
            CharacteristicProperties = GattCharacteristicProperties.Write | GattCharacteristicProperties.WriteWithoutResponse,
            WriteProtectionLevel = GattProtectionLevel.Plain,
            UserDescription = "Actuate Characteristc",
        };
        private static async Task<bool> IsPeripheralModeSupported()
        {
            // BT_Code: New for Creator's Update - Bluetooth adapter has properties of the local BT radio.
            var localAdapter = await BluetoothAdapter.GetDefaultAsync();

            if (localAdapter != null)
            {
                return localAdapter.IsPeripheralRoleSupported;
            }
            else
            {
                // Bluetooth is not turned on 
                return false;
            }
        }
        private async Task<GattLocalCharacteristic> MakeCharacteristic(Guid guid, GattLocalCharacteristicParameters parameters)
        {
            GattLocalCharacteristicResult result = await serviceProvider.Service.CreateCharacteristicAsync(guid, parameters);
            if (result.Error != BluetoothError.Success)
            {
                Console.WriteLine("Error creating characteristic");
                return null;
            }
            return result.Characteristic;
        }
        public async Task<bool> start()
        {
            if (!await IsPeripheralModeSupported())
            {
                Console.WriteLine("Error, bluetooth peripheral mode not supported");
                return false;
            }
            GattServiceProviderResult serviceResult = await GattServiceProvider.CreateAsync(serviceID);
            if (serviceResult.Error != BluetoothError.Success)
            {
                Console.WriteLine("Error creating service provider");
                return false;
            }

            serviceProvider = serviceResult.ServiceProvider;

            DAQCharacteristic = await MakeCharacteristic(daqID, ReadParams);
            actuateCharacteristic = await MakeCharacteristic(actuateID, ActuateParams);

            DAQCharacteristic.ReadRequested += DAQReadAsync;
            actuateCharacteristic.WriteRequested += ActuateWriteAsync;

            GattServiceProviderAdvertisingParameters advParams = new GattServiceProviderAdvertisingParameters
            {
                IsConnectable = true,
                IsDiscoverable = true,
            };

            serviceProvider.AdvertisementStatusChanged += AdvertismentStatusChanged;
            serviceProvider.StartAdvertising(advParams);
            return true;
        }


        private void AdvertismentStatusChanged(GattServiceProvider sender, GattServiceProviderAdvertisementStatusChangedEventArgs args)
        {
            Console.WriteLine($"New Advertising Status: {sender.AdvertisementStatus}");
        }

        private async void DAQReadAsync(GattLocalCharacteristic sender, GattReadRequestedEventArgs args)
        {
            using (args.GetDeferral())
            {
                GattReadRequest request = await args.GetRequestAsync();
                if (request == null)
                {
                    Console.WriteLine("Access to device not allowed (does the app have bluetooth permissions?)");
                    return;
                }

                var writer = new DataWriter();
                writer.ByteOrder = ByteOrder.LittleEndian;
                writer.WriteSingle(force);
                writer.WriteSingle(pressure1);
                writer.WriteSingle(pressure2);
                writer.WriteBoolean(ball_valve_engaged);
                writer.WriteBoolean(ball_valve_open);
                writer.WriteBoolean(is_armed);
                request.RespondWithValue(writer.DetachBuffer());
            }
        }
        private async void ActuateWriteAsync(GattLocalCharacteristic sender, GattWriteRequestedEventArgs args)
        {
            using (args.GetDeferral())
            {
                // Get the request information.  This requires device access before an app can access the device's request.
                GattWriteRequest request = await args.GetRequestAsync();
                if (request == null)
                {
                    // No access allowed to the device.  Application should indicate this to the user.
                    return;
                }

                if (request.Value.Length != 4)
                {
                    if (request.Option == GattWriteOption.WriteWithResponse)
                    {
                        request.RespondWithProtocolError(GattProtocolError.InvalidAttributeValueLength);
                    }
                    return;
                }

                var reader = DataReader.FromBuffer(request.Value);
                reader.ByteOrder = ByteOrder.LittleEndian;
                int val = reader.ReadInt32();

                if (val == OpenCode)
                {
                    Console.WriteLine("OPEN");
                    action = BallValveAction.Open;
                }
                else if (val == CloseCode)
                {
                    Console.WriteLine("CLOSE");
                    action = BallValveAction.Close;
                }
                else if (val == ArmCode)
                {
                    Console.WriteLine("ARM");
                    action = BallValveAction.Arm;
                } 
                else if (val == DisarmCode)
                {
                    Console.WriteLine("DISARM");
                    action = BallValveAction.Disarm;
                } 
                else if (val == FireCode)
                {
                    Console.WriteLine("FIRE");
                    action = BallValveAction.Fire;
                }
                else {
                    Console.WriteLine($"Unrecognized val {val}");
                }
                if (request.Option == GattWriteOption.WriteWithResponse)
                {
                    request.Respond();
                }
            }
        }
    }
}