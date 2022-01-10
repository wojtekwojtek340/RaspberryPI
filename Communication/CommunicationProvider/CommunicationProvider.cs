using Microsoft.Azure.Devices;
using Microsoft.Azure.Devices.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Communication.CommunicationProvider
{
    public class CommunicationProvider
    {
        #region Wifi settings
        private readonly ServiceClient serviceClient;
        private readonly DeviceClient device1Client;
        private const string connectionStringService = "HostName=IoTPwr.azure-devices.net;SharedAccessKeyName=service;SharedAccessKey=ei4kpLbA2fAC2hDxnXYuFR+d6Ig8FPeA0s1FCsZuKvQ=";
        private const string connectionStringDevice1 = "HostName=IoTPwr.azure-devices.net;DeviceId=TestDevice;SharedAccessKey=QPWitVzCVJDpxp6fX9X5zmryopfi2WCuf5MEQ+M9/D8=";
        private const string targetDevice = "TestDevice2";
        #endregion

        public CommunicationProvider()
        {
            //stworzenie obiektów do obsługi komunikacji Wifi
            serviceClient = ServiceClient.CreateFromConnectionString(connectionStringService);
            device1Client = DeviceClient.CreateFromConnectionString(connectionStringDevice1);
        }

        /// <summary>
        /// Metoda do wysyłania danych.
        /// </summary>
        /// <returns></returns>
        public async Task SendCloudToDeviceMessageAsync<T>(T message)
        {
            var request = JsonConvert.SerializeObject(message);
            var commandMessage = new Microsoft.Azure.Devices.Message(Encoding.ASCII.GetBytes(request));
            await serviceClient.SendAsync(targetDevice, commandMessage);
        }

        /// <summary>
        /// Metoda do odbierania danych.
        /// </summary>
        /// <returns></returns>
        public async Task<T> ReceiveAsync<T>()
        {
            while (true)
            {
                //odczyt danych
                Microsoft.Azure.Devices.Client.Message receivedMessage = await device1Client.ReceiveAsync();
                if (receivedMessage == null) continue;

                //konwersja danych po odczycie do obiektu User.
                var response = JsonConvert.DeserializeObject<T>(Encoding.ASCII.GetString(receivedMessage.GetBytes()));

                //tak jakby zakończenie odczytu informacji z IoT Hub.
                await device1Client.CompleteAsync(receivedMessage);

                //jeżeli odczytana wiadomość jest różna od nula to wychodzimy z tąd.
                if (receivedMessage != null)
                {
                    return response;
                }
            }
        }
    }
}
