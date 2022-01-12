using Communication.CommunicationProvider;
using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using PI.HomeAlgorithm;
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
        static void Main(string[] args)
        {
            //Console.WriteLine("Oczekiwanie na dołączenie do debugera");
            //while (!Debugger.IsAttached)
            //{
            //    Thread.Sleep(300);
            //}
            //Console.WriteLine("Podłączono debuger");

            CommunicationProvider communication = new Communication();

            Home homeAlgorithm = new Home();
            homeAlgorithm.Start();                      
        }         
    }
}
