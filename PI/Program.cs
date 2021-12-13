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
        static readonly int PIN_02 = 2;
        static readonly int PIN_03 = 3;
        static readonly int PIN_04 = 4;
        static readonly int PIN_14 = 14;
        static int fazaSilnika = 0;
        public const double Hgr = 80; //graniczna wilgotność przy której włączana i wyłączana jest wentylacja
        public const double Twgr = 16; //graniczna temp. wewn. domku przy której włączane i wyłączane jest grzanie
        public const double Tzgr = 10; //graniczna temp. zewn. od której zależy czy grzanie zostanie włączone przy uruchomionej wentylacji
        public const double tFood = 43200; //standardowa godzina podania jedzenia kotu wyrażona w sekundach np. 62 to 00:01:02
        public const double maxFoodDelay = 3600; //maksymalne opóźnienie podania jedzenia wynikające z obecności kota w domku 
        static bool isDoorOpen = true;
        static bool isHeatingOn = false;
        static bool isVentilationOn = false;
        static bool wasFoodGivenToday = false;
        static double Tz = 16;
        static double Tw = 16;
        static double H = 75;
        static double catMass = 5;
        static double weight = 5;
        static int time = -1; //czas w sekundach danej doby
        #endregion
        static void Main(string[] args)
        {
            controller = new GpioController();
            controller.OpenPin(PIN_02, PinMode.Output);
            controller.OpenPin(PIN_03, PinMode.Output);
            controller.OpenPin(PIN_04, PinMode.Output);
            controller.OpenPin(PIN_14, PinMode.Output);
            fazaSilnika = 0;
            controller.Write(PIN_02, 1);
            controller.Write(PIN_03, 0);
            controller.Write(PIN_04, 0);
            controller.Write(PIN_14, 0);
            while (true) //test silnika krokowego
            {
                Console.WriteLine("krok ");
                switch (fazaSilnika)
                {
                    case 0: //1000 jest i zmiana na case 1 itd.
                        controller.Write(PIN_03, 1);
                        fazaSilnika++;
                        break;
                    case 1: //1100
                        controller.Write(PIN_02, 0);
                        fazaSilnika++;
                        break;
                    case 2: //0100
                        controller.Write(PIN_04, 1);
                        fazaSilnika++;
                        break;
                    case 3: //0110
                        controller.Write(PIN_03, 0);
                        fazaSilnika++;
                        break;
                    case 4: //0010
                        controller.Write(PIN_14, 1);
                        fazaSilnika++;
                        break;
                    case 5: //0011
                        controller.Write(PIN_04, 0);
                        fazaSilnika++;
                        break;
                    case 6: //0001
                        controller.Write(PIN_02, 1);
                        fazaSilnika++;
                        break;
                    case 7: //1001
                        controller.Write(PIN_14, 0);
                        fazaSilnika = 0;
                        break;
                    default:
                        break;
                }
                Thread.Sleep(1);
            }
            Console.WriteLine("Oczekiwanie na dołączenie do debugera");
            while (!Debugger.IsAttached)
            {
                Thread.Sleep(300);
            }
            Console.WriteLine("Podłączono debuger");


            //stworzenie obiektu do obsługi raspberry
            //controller = new GpioController();            
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

                // otwieranie i zamykanie blokady drzwi
                if ((RFID() == true) && (isDoorOpen = false))
                {
                    isDoorOpen = true;
                    Door(isDoorOpen);
                }
                else if ((RFID() == false) && (isDoorOpen = true))
                {
                    isDoorOpen = false;
                    Door(isDoorOpen);
                }

                // obsługa wodopoju
                // TBD

                //aktualizowanie godziny
                if (GetTime() < time)
                {
                    time = GetTime();
                    wasFoodGivenToday = false;
                }
                else time = GetTime();

                // obsługa podajnika jedzenia
                if ((wasFoodGivenToday == false) && (time > tFood) && ((Presence() == false) || (time > (tFood + maxFoodDelay)))) //obecnosc kota niechciana ze względu na utrudnienie pomiaru masy podanego pokarmu
                {
                    // podanie jedzenia
                    // TBD
                    wasFoodGivenToday = true;
                }

                // ważenie kota
                weight = Weight();
                if (weight > 0.80*catMass) // założono, że masa kota nie może zmaleć więcej niż o 20% od ostatniego pomiaru
                {
                    catMass = weight;
                }

                // sterowanie ogrzewaniem i wentylacją
                H = Humidity();
                Tz = TempOut();
                Tw = TempIn();
                if ((H > Hgr) && (Tz >= Tzgr)) // grzanie-0, wentylacja-1
                {
                    if(isHeatingOn == true)
                    {
                        isHeatingOn = false;
                        Heating(isHeatingOn);
                    }
                    if (isVentilationOn == false)
                    {
                        isVentilationOn = true;
                        Ventilation(isVentilationOn);
                    }
                }
                else if (((H > Hgr) && (Tz < Tzgr)) || (Tw < Twgr)) // grzanie-1, wentylacja-1
                {
                    if (isHeatingOn == false)
                    {
                        isHeatingOn = true;
                        Heating(isHeatingOn);
                    }
                    if (isVentilationOn == false)
                    {
                        isVentilationOn = true;
                        Ventilation(isVentilationOn);
                    }
                }
                else // grzanie-0, wentylacja-0
                {
                    if (isHeatingOn == true)
                    {
                        isHeatingOn = false;
                        Heating(isHeatingOn);
                    }
                    if (isVentilationOn == true)
                    {
                        isVentilationOn = false;
                        Ventilation(isVentilationOn);
                    }
                }
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

        static double TempIn() // temperatura wewnątrz domku
        {
            //odczytanie temp. wewn. domku
            // TBD
            double Tw = 20; // TYMCZASOWO USTAWIONA WARTOŚĆ
            return Tw;
        }

        static double TempOut() // temperatura na zewnątrz domku
        {
            //odczytanie temp. zewn.
            // TBD
            double Tz = 15; // TYMCZASOWO USTAWIONA WARTOŚĆ
            return Tz;
        }

        static double Humidity() // wilgotność wewnątrz domku
        {
            //odczytanie wilgotnosci
            // TBD
            double H = 85; // TYMCZASOWO USTAWIONA WARTOŚĆ
            return H;
        }

        static double Weight() // wskazanie wagi
        {
            //TBD
            double m = 5; // TYMCZASOWO USTAWIONA WARTOŚĆ
            return m;
        }

        static bool Presence() // 1 - stwierdzono obecnosc kota w domku, 0 - nie stwierdzono
        {
            //sprawdzenie obecnosci
            // TBD
            bool presence = true; // TYMCZASOWO USTAWIONA WARTOŚĆ
            return presence;
        }

        static bool RFID() // 1 - wykryto znacznik RFID kota, 0 - nie wykryto
        {
            //sprawdzenie RFID
            // TBD
            bool rfid = true; // TYMCZASOWO USTAWIONA WARTOŚĆ
            return rfid;
        }

        static void Door(bool isOpen) // 1 - odblokuj drzwi, 0 - zablokuj drzwi
        {
            if (isOpen) //otwieranie drzwi 
            {
                //TBD
            }
            else //zamykanie drzwi  
            {
                //TBD
            }
        }

        static void Heating(bool isOn) // 1 - wlącz grzanie, 0 - wyłącz grzanie
        {
            if (isOn) //wlaczanie spirali grzejnej
            {
                //TBD
            }
            else //wylaczanie spirali grzejnej
            {
                //TBD
            }
        }

        static void Ventilation(bool isOn) // 1 - włącz wentylację, 0 - wyłącz wentylację
        {
            if (isOn) //wlaczenie wentylacji
            {
                //TBD
            }
            else //wylaczenie wentylacji
            {
                //TBD
            }
        }

        static int GetTime()
        {
            string s = DateTime.Now.ToString("T");
            // odpowiednia konwersja zmiennej typu string na int - trzeba sprawdzić format, może przydać się atoi()
            //TBD
            int t = 10; //TYMACZASOWO WPISANA WARTOŚĆ
            return t;
        } // zwraca pore dnia w sekundach
    }
}
