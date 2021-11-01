using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using System;
using System.Device.Gpio;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PI
{
    /// <summary>
    /// główna klasa startowa
    /// </summary>
    class Program
    {
        #region Wifi
        static ServiceClient serviceClient;
        static DeviceClient device1Client;        
        static readonly string connectionStringService = "HostName=IoTPwr.azure-devices.net;SharedAccessKeyName=service;SharedAccessKey=ei4kpLbA2fAC2hDxnXYuFR+d6Ig8FPeA0s1FCsZuKvQ=";
        static readonly string connectionStringDevice1 = "HostName=IoTPwr.azure-devices.net;DeviceId=TestDevice;SharedAccessKey=QPWitVzCVJDpxp6fX9X5zmryopfi2WCuf5MEQ+M9/D8=";
        static readonly string targetDevice = "TestDevice2";
        #endregion

        #region Raspberry
        static GpioController controller;
        static readonly int PIN_04 = 4;
        #endregion
        static void Main(string[] args)
        {
            Console.WriteLine("Oczekiwanie na dołączenie do debugera");
            while (!Debugger.IsAttached)
            {
                Thread.Sleep(300);
            }
            Console.WriteLine("Podłączono debuger");


            //stworzenie obiektu do obsługi raspberry
            controller = new GpioController();            
            controller.OpenPin(PIN_04, PinMode.Output);

            //stworzenie obiektów do obsługi komunikacji Wifi
            serviceClient = ServiceClient.CreateFromConnectionString(connectionStringService);
            device1Client = DeviceClient.CreateFromConnectionString(connectionStringDevice1);         

            //główna pętla programu
            while (true)
            {
                //oczekiwanie na odczyt danych przez Wifi
                ReceiveAsync().Wait();
                Console.WriteLine("Oczekiwanie");
            }
        }

        /// <summary>
        /// Metoda do wysyłania danych.
        /// </summary>
        /// <returns></returns>
        private async static Task SendCloudToDeviceMessageAsync()
        {
            var commandMessage = new Microsoft.Azure.Devices.Message(Encoding.ASCII.GetBytes($"Urzadzenie 1 do urzadzenia 2. {DateTime.Now}"));
            await serviceClient.SendAsync(targetDevice, commandMessage);
        }

        /// <summary>
        /// Metoda do odbierania danych.
        /// </summary>
        /// <returns></returns>
        private static async Task ReceiveAsync()
        {
            while (true)
            {
                //odczyt danych
                Microsoft.Azure.Devices.Client.Message receivedMessage = await device1Client.ReceiveAsync();
                if (receivedMessage == null) continue;

                //konwersja danych po odczycie do obiektu User.
                var response = JsonConvert.DeserializeObject<User>(Encoding.ASCII.GetString(receivedMessage.GetBytes()));

                //wypisanie tekstu na konsole.
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Ilosc: {response.Count} Data: {response.Now}");
                Console.ResetColor();            

                //świecenia lampką.
                for (int i = 0; i < response.Count; i++)
                {
                    controller.Write(PIN_04, PinValue.High);
                    Thread.Sleep(100);
                    controller.Write(PIN_04, PinValue.Low);
                    Thread.Sleep(100);
                }

                //tak jakby zakończenie odczytu informacji z IoT Hub.
                await device1Client.CompleteAsync(receivedMessage);

                //jeżeli odczytana wiadomość jest różna od nula to wychodzimy z tąd.
                if (receivedMessage != null) return;
            }
        }
    }
}
//TEST
