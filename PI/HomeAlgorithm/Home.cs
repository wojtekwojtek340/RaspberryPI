using System;
using System.Collections.Generic;
using System.Device.Gpio;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Communication.CommunicationProvider;
using Communication.CommunicationProvider.Messages;
using Iot.Device.DHTxx;
using System.Runtime.InteropServices;

namespace PI.HomeAlgorithm
{
    public class Home
    {
        #region Raspberry
        GpioController controller;
        CommunicationProvider CommunicationProvider { get; }
        CommunicationCommands Command { get; set; }
        readonly int PIN_02 = 8; //Piny silnika krokowego nr 1 - blokada drzwi
        readonly int PIN_03 = 7;
        readonly int PIN_04 = 4;
        readonly int PIN_14 = 14;
        int motorPhase1 = 0;
        readonly int PIN_15 = 15; //Piny silnika krokowego nr 2 - podajnik pokarmu
        readonly int PIN_17 = 17;
        readonly int PIN_18 = 18;
        readonly int PIN_27 = 27;
        int motorPhase2 = 0;
        readonly int PIN_22 = 22; //Pin sterowania zaworem odcinającym odpływ cieczy z miski
        readonly int PIN_23 = 23; //Pin sterowania pompką
        readonly int PIN_24 = 24; //Pin czujnika obecności HC-SR501
        readonly int PIN_10 = 10; //Pin czujnika RFID
        readonly int PIN_09 = 9;  //Pin spirali grzejnej
        readonly int PIN_25 = 25; //Pin sterowania wentylacją 
        readonly int PIN_11 = 11; //Pin czujnika temperatury i wilgotności
        Dht11 sensor1;
        private const double Hgr = 80; //graniczna wilgotność przy której włączana i wyłączana jest wentylacja
        private const double Twgr = 16; //graniczna temp. wewn. domku przy której włączane i wyłączane jest grzanie
        private const double Tzgr = 10; //graniczna temp. zewn. od której zależy czy grzanie zostanie włączone przy uruchomionej wentylacji
        private const double tFood = 43200; //standardowa godzina podania jedzenia kotu wyrażona w sekundach np. 62 to 00:01:02
        private const double tWater = 43200; //standardowa godzina wymiany wody
        private const double maxFoodDelay = 3600; //maksymalne opóźnienie podania jedzenia wynikające z obecności kota w domku 
        private const double maxRFIDDelay = 120; //maksymalne opóźnienie zamknięcia drzwi
        private double RFIDTime = -1; //godzina do której mogą być otwarte drzwi
        bool isDoorOpen = true;
        bool isHeatingOn = false;
        bool isVentilationOn = false;
        bool wasFoodGivenToday = false;
        double Tz = 16;
        double Tw = 16;
        double H = 75;
        double catMass = 0;
        double weight = 0;
        int time = -1; //czas w sekundach danej doby
        //I2c
        int OPEN_READ_WRITE = 2;
        int I2C_SLAVE = 0x0703;
        [DllImport("libc.so.6", EntryPoint = "open")]
        public static extern int Open(string fileName, int mode);
        [DllImport("libc.so.6", EntryPoint = "ioctl", SetLastError = true)]
        private extern static int Ioctl(int fd, int request, int data);
        [DllImport("libc.so.6", EntryPoint = "read", SetLastError = true)]
        internal static extern int Read(int handle, byte[] data, int length);
        #endregion

        public Home(CommunicationProvider communicationProvider)
        {
            CommunicationProvider = communicationProvider;
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
                var response = CommunicationProvider.ReceiveAsync<Message>();

                Command = response.Result.Command;
                
                if(Command == CommunicationCommands.ParametersInfo)
                {
                    CommunicationProvider.SendCloudToDeviceMessageAsync(new Message()).Wait();
                }
                else if(Command == CommunicationCommands.GiveFood)
                {
                    motorPhase2 = MotorMove(true, motorPhase2, 9000, true, 1, PIN_15, PIN_17, PIN_18, PIN_27);
                }
                else if (Command == CommunicationCommands.GiveWatter)
                {
                    controller.Write(PIN_22, 1); //otwarcie zaworu
                    Thread.Sleep(5000);
                    controller.Write(PIN_22, 0); //zamknięcie zaworu
                }
                else if (Command == CommunicationCommands.ResetHome)
                {
                    Environment.Exit(0);
                }

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

        int MotorMove(bool direction, int motorPhase, int steps, bool halfStepsOn, int stepTime, int PIN_A, int PIN_B, int PIN_C, int PIN_D)
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

        double TempIn() // temperatura wewnątrz domku
        {
            //odczytanie temp. wewn. domku
            double Tw = Convert.ToDouble($"{sensor1.Temperature.DegreesCelsius:0.#}");
            return Tw;
        }

        double TempOut() // temperatura na zewnątrz domku
        {
            double Tz = 0; //zrezygnowano z tego czujnika, więc przyjęto wartość zapewniającą włączenie ogrzewania przy wentylacji
            return Tz;
        }

        double Humidity() // wilgotność wewnątrz domku
        {
            //odczytanie wilgotnosci
            double H = Convert.ToDouble($"{sensor1.Humidity:0.#}");
            return H;
        }

        double Weight() // wskazanie wagi
        {
            // ustawienie czytania z magistrali I2c nr 1
            var i2cBushandle = Open("/dev/i2c-1", OPEN_READ_WRITE); 
            // otwarcie komunikacji z układem slave o adresie 0x48
            int registerAddress = 0x48;
            var deviceReturnCode = Ioctl(i2cBushandle, I2C_SLAVE, registerAddress);
            // wczytywanie 10 bitów z urządzenia do tablicy
            var deviceDataInMemory = new byte[10];
            Read(i2cBushandle, deviceDataInMemory, deviceDataInMemory.Length);
            double sum = 0;
            for(int i = 0; i < 10; i++)
            {
                sum += Math.Pow(2,9-i) * deviceDataInMemory[i];
            }
            double m = sum / 1023 * 10;
            return m;
        }

        bool Presence() // 1 - stwierdzono obecnosc kota w domku, 0 - nie stwierdzono
        {
            //sprawdzenie obecnosci
            return PinValue.High.Equals(controller.Read(PIN_24));
        }

        bool RFID() // 1 - wykryto znacznik RFID kota, 0 - nie wykryto
        {
            //sprawdzenie RFID
            return PinValue.High.Equals(controller.Read(PIN_10));
        }

        void Door(bool isOpen) // 1 - odblokuj drzwi, 0 - zablokuj drzwi
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

        void Heating(bool isOn) // 1 - wlącz grzanie, 0 - wyłącz grzanie
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

        void Ventilation(bool isOn) // 1 - włącz wentylację, 0 - wyłącz wentylację
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

        int GetTime() // zwraca pore dnia w sekundach
        {
            int t = (int)(DateTime.Now - DateTime.Today).TotalSeconds;
            return t;
        }
    }
}
