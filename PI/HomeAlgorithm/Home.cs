using System;
using System.Collections.Generic;
using System.Device.Gpio;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Iot.Device.DHTxx;

namespace PI.HomeAlgorithm
{
    public class Home
    {
        #region Raspberry
        static GpioController controller;
        static readonly int PIN_02 = 2; //Piny silnika krokowego nr 1 - blokada drzwi
        static readonly int PIN_03 = 3;
        static readonly int PIN_04 = 4;
        static readonly int PIN_14 = 14;
        static int motorPhase1 = 0;
        static readonly int PIN_15 = 15; //Piny silnika krokowego nr 2 - podajnik pokarmu
        static readonly int PIN_17 = 17;
        static readonly int PIN_18 = 18;
        static readonly int PIN_27 = 27;
        static int motorPhase2 = 0;
        static readonly int PIN_22 = 22; //Pin sterowania zaworem odcinającym odpływ cieczy z miski
        static readonly int PIN_23 = 23; //Pin sterowania pompką
        static readonly int PIN_24 = 24; //Pin czujnika obecności HC-SR501
        static readonly int PIN_10 = 10; //Pin czujnika RFID
        static readonly int PIN_09 = 9;  //Pin spirali grzejnej
        static readonly int PIN_25 = 25; //Pin sterowania wentylacją 
        static readonly int PIN_11 = 11; //Pin czujnika temperatury i wilgotności
        static Dht11 sensor1;
        public const double Hgr = 80; //graniczna wilgotność przy której włączana i wyłączana jest wentylacja
        public const double Twgr = 16; //graniczna temp. wewn. domku przy której włączane i wyłączane jest grzanie
        public const double Tzgr = 10; //graniczna temp. zewn. od której zależy czy grzanie zostanie włączone przy uruchomionej wentylacji
        public const double tFood = 43200; //standardowa godzina podania jedzenia kotu wyrażona w sekundach np. 62 to 00:01:02
        public const double tWater = 43200; //standardowa godzina wymiany wody
        public const double maxFoodDelay = 3600; //maksymalne opóźnienie podania jedzenia wynikające z obecności kota w domku 
        public const double maxRFIDDelay = 120; //maksymalne opóźnienie zamknięcia drzwi
        public double RFIDTime = -1; //godzina do której mogą być otwarte drzwi
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
            motorPhase1 = 0;
            controller.Write(PIN_02, 1);
            controller.Write(PIN_03, 0);
            controller.Write(PIN_04, 0);
            controller.Write(PIN_14, 0);
            controller.OpenPin(PIN_15, PinMode.Output);
            controller.OpenPin(PIN_17, PinMode.Output);
            controller.OpenPin(PIN_18, PinMode.Output);
            controller.OpenPin(PIN_27, PinMode.Output);
            motorPhase2 = 0;
            controller.Write(PIN_15, 1);
            controller.Write(PIN_17, 0);
            controller.Write(PIN_18, 0);
            controller.Write(PIN_27, 0);
            controller.OpenPin(PIN_22, PinMode.Output);
            controller.OpenPin(PIN_23, PinMode.Output);
            controller.Write(PIN_22, 0);
            controller.Write(PIN_23, 0);
            controller.OpenPin(PIN_24, PinMode.Input);
            controller.OpenPin(PIN_10, PinMode.Input);
            controller.OpenPin(PIN_09, PinMode.Output);
            controller.Write(PIN_09, 0);
            controller.OpenPin(PIN_25, PinMode.Output);
            controller.Write(PIN_25, 0);
            sensor1 = new Dht11(PIN_11);
        }
        public void Start()
        {
            //główna pętla programu
            while (true)
            {
                //aktualizowanie godziny
                if (GetTime() < time)
                {
                    time = GetTime();
                    wasFoodGivenToday = false;
                }
                else time = GetTime();

                // otwieranie i zamykanie blokady drzwi
                if ((RFID() == true) && (isDoorOpen = false))
                {
                    isDoorOpen = true;
                    Door(isDoorOpen);
                    RFIDTime = time;
                }
                else if ((RFID() == true) && (isDoorOpen = true))
                {
                    RFIDTime = time;
                }
                else if ((RFID() == false) && (isDoorOpen = true))
                {
                    if ((RFIDTime!=(-1)) && (((time < RFIDTime) && (time + 86400 - RFIDTime > maxRFIDDelay)) || ((time >= RFIDTime) && (time - RFIDTime > maxRFIDDelay))))
                    {
                        RFIDTime = -1;
                        isDoorOpen = false;
                        Door(isDoorOpen);
                    }
                }

                // obsługa wodopoju
                if (time > tWater)
                {
                    controller.Write(PIN_22, 1); //otwarcie zaworu
                    Thread.Sleep(5000);
                    controller.Write(PIN_22, 0); //zamknięcie zaworu
                    controller.Write(PIN_23, 1); //uruchomienie pompki
                    Thread.Sleep(5000);
                    controller.Write(PIN_22, 0); //wyłączenie pompki
                }

                // obsługa podajnika jedzenia
                if ((wasFoodGivenToday == false) && (time > tFood) && ((Presence() == false) || (time > (tFood + maxFoodDelay)))) //obecnosc kota niechciana ze względu na utrudnienie pomiaru masy podanego pokarmu
                {
                    // podanie jedzenia
                    motorPhase2 = MotorMove(true, motorPhase2, 9000, true, 1, PIN_15, PIN_17, PIN_18, PIN_27);
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

        static int MotorMove(bool direction, int motorPhase, int steps, bool halfStepsOn, int stepTime, int PIN_A, int PIN_B, int PIN_C, int PIN_D)
        {//direction=true - zgodny z regułą prawej dłoni, dla zwrotu wyjścia wału z obudowy, stepTime - czas pomiędzy kolejnymi krokami
            int stepsMade = 0;
            if (direction == true)
            {
                while (stepsMade < steps)
                {
                    switch (motorPhase)
                    {
                        case 0: //1000 jest i zmiana na case 1 itd.
                            controller.Write(PIN_B, 1);
                            motorPhase++;
                            break;
                        case 1: //1100
                            controller.Write(PIN_A, 0);
                            motorPhase++;
                            break;
                        case 2: //0100
                            controller.Write(PIN_C, 1);
                            motorPhase++;
                            break;
                        case 3: //0110
                            controller.Write(PIN_B, 0);
                            motorPhase++;
                            break;
                        case 4: //0010
                            controller.Write(PIN_D, 1);
                            motorPhase++;
                            break;
                        case 5: //0011
                            controller.Write(PIN_C, 0);
                            motorPhase++;
                            break;
                        case 6: //0001
                            controller.Write(PIN_A, 1);
                            motorPhase++;
                            break;
                        case 7: //1001
                            controller.Write(PIN_D, 0);
                            motorPhase = 0;
                            break;
                        default:
                            break;
                    }
                    stepsMade++;
                    if(halfStepsOn || (stepsMade % 2 == 0))
                    {
                        Thread.Sleep(stepTime);
                    }
                }
            }
            else
            {
                switch (motorPhase)
                {
                    case 0: //1000 jest i zmiana na case 1 itd.
                        controller.Write(PIN_D, 1);
                        motorPhase = 7;
                        break;
                    case 1: //1100
                        controller.Write(PIN_B, 0);
                        motorPhase--;
                        break;
                    case 2: //0100
                        controller.Write(PIN_A, 1);
                        motorPhase--;
                        break;
                    case 3: //0110
                        controller.Write(PIN_C, 0);
                        motorPhase--;
                        break;
                    case 4: //0010
                        controller.Write(PIN_B, 1);
                        motorPhase--;
                        break;
                    case 5: //0011
                        controller.Write(PIN_D, 0);
                        motorPhase--;
                        break;
                    case 6: //0001
                        controller.Write(PIN_C, 1);
                        motorPhase--;
                        break;
                    case 7: //1001
                        controller.Write(PIN_A, 0);
                        motorPhase--;
                        break;
                    default:
                        break;
                }
                stepsMade++;
                if (halfStepsOn || (stepsMade % 2 == 0))
                {
                    Thread.Sleep(stepTime);
                }
            }
            return motorPhase;
        }

        static double TempIn() // temperatura wewnątrz domku
        {
            //odczytanie temp. wewn. domku
            double Tw = Convert.ToDouble($"{sensor1.Temperature.DegreesCelsius:0.#}");
            return Tw;
        }

        static double TempOut() // temperatura na zewnątrz domku
        {
            double Tz = 0; //zrezygnowano z tego czujnika, więc przyjęto wartość zapewniającą włączenie ogrzewania przy wentylacji
            return Tz;
        }

        static double Humidity() // wilgotność wewnątrz domku
        {
            //odczytanie wilgotnosci
            double H = Convert.ToDouble($"{sensor1.Humidity:0.#}");
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
            return PinValue.High.Equals(controller.Read(PIN_24));
        }

        static bool RFID() // 1 - wykryto znacznik RFID kota, 0 - nie wykryto
        {
            //sprawdzenie RFID
            return PinValue.High.Equals(controller.Read(PIN_10));
        }

        static void Door(bool isOpen) // 1 - odblokuj drzwi, 0 - zablokuj drzwi
        {
            if (isOpen) //otwieranie drzwi 
            {
                motorPhase1 = MotorMove(true, motorPhase1, 1125, false, 1, PIN_02, PIN_03, PIN_04, PIN_14);
            }
            else //zamykanie drzwi  
            {
                motorPhase1 = MotorMove(false, motorPhase1, 1125, false, 1, PIN_02, PIN_03, PIN_04, PIN_14);
            }
        }

        static void Heating(bool isOn) // 1 - wlącz grzanie, 0 - wyłącz grzanie
        {
            if (isOn) //wlaczanie spirali grzejnej
            {
                controller.Write(PIN_09, 1);
            }
            else //wylaczanie spirali grzejnej
            {
                controller.Write(PIN_09, 0);
            }
        }

        static void Ventilation(bool isOn) // 1 - włącz wentylację, 0 - wyłącz wentylację
        {
            if (isOn) //wlaczenie wentylacji
            {
                controller.Write(PIN_25, 1);
            }
            else //wylaczenie wentylacji
            {
                controller.Write(PIN_25, 0);
            }
        }

        static int GetTime() // zwraca pore dnia w sekundach
        {
            int t = (int)(DateTime.Now - DateTime.Today).TotalSeconds;
            return t;
        }
    }
}
