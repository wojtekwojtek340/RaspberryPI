using System;
using System.Collections.Generic;
using System.Device.Gpio;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PI.HomeAlgorithm
{
    public class Home
    {
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

        public Home()
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
        }
        public void Start()
        {
            //główna pętla programu
            while (true)
            {                              
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
                if (weight > 0.80 * catMass) // założono, że masa kota nie może zmaleć więcej niż o 20% od ostatniego pomiaru
                {
                    catMass = weight;
                }

                // sterowanie ogrzewaniem i wentylacją
                H = Humidity();
                Tz = TempOut();
                Tw = TempIn();
                if ((H > Hgr) && (Tz >= Tzgr)) // grzanie-0, wentylacja-1
                {
                    if (isHeatingOn == true)
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

        private void MotorTest()
        {
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
                Thread.Sleep(10);
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
