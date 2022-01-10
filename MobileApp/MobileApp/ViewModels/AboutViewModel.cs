using Communication.CommunicationProvider;
using Communication.CommunicationProvider.Messages;
using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace MobileApp.ViewModels
{
    public class AboutViewModel : BaseViewModel
    {
        private readonly CommunicationProvider communicationProvider;

        private string cameraUri;
        public string CameraUri
        {
            get { return cameraUri; }
            set { SetProperty(ref cameraUri, value); }
        }

        private double temperature;
        public double Temperature
        {
            get { return temperature; }
            set { SetProperty(ref temperature, value); }
        }

        private double preasure;
        public double Preasure
        {
            get { return preasure; }
            set { SetProperty(ref preasure, value); }
        }

        private bool isPlaying;
        public bool IsPlaying
        {
            get { return isPlaying; }
            set { SetProperty(ref isPlaying, value); }
        }

        private double humidity;
        public double Humidity
        {
            get { return humidity; }
            set { SetProperty(ref humidity, value); }
        }

        private double catWeight;
        public double CatWeight
        {
            get { return catWeight; }
            set { SetProperty(ref catWeight, value); }
        }

        private string videoInfo;

        public string VideoInfo

        {
            get { return videoInfo; }
            set { SetProperty(ref videoInfo, value); }
        }



        public AboutViewModel()
        {
            communicationProvider = new CommunicationProvider();

            Title = "Strona główna";
            Temperature = 23;
            Preasure = 1023;
            Humidity = 45;
            CatWeight = 3.34;
            StartVideo();

            OpenWebCommand = new Command(async () => await Browser.OpenAsync("https://aka.ms/xamarin-quickstart"));
            StartVideoCommand = new Command(() => StartVideo());
            SendWaterCommand = new Command(() => SendWater());
            SendFoodCommand = new Command(() => SendFood());
            ResetHomeCommand = new Command(() => ResetHome());
        }

        private void ResetHome()
        {
            var request = new Message { Command = CommunicationCommands.ResetHome, Now = DateTime.Now };
            Task.Run(() => communicationProvider.SendCloudToDeviceMessageAsync(request));
        }

        private void SendWater()
        {
            var request = new Message { Command = CommunicationCommands.GiveWatter, Now = DateTime.Now };
            Task.Run(() => communicationProvider.SendCloudToDeviceMessageAsync(request));
        }

        private void SendFood()
        {
            var request = new Message { Command = CommunicationCommands.GiveFood, Now = DateTime.Now };
            Task.Run(() => communicationProvider.SendCloudToDeviceMessageAsync(request));
            var messsage = communicationProvider.ReceiveAsync<Message>();
        }

        private void StartVideo()
        {
            if (IsPlaying)
            {
                VideoInfo = "WŁĄCZ STREAM";
                IsPlaying = false;
            }
            else
            {
                VideoInfo = "WYŁĄCZ STREAM";
                IsPlaying = true;
            }
        }

        public ICommand OpenWebCommand { get; }
        public ICommand StartVideoCommand { get; }
        public ICommand SendWaterCommand { get; }
        public ICommand SendFoodCommand { get; }
        public ICommand ResetHomeCommand { get; }
    }
}