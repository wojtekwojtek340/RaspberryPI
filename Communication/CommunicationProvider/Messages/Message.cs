using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Communication.CommunicationProvider.Messages
{
    /// <summary>
    /// taka klasa testowa do obsługi odczytu danych wysyałanych do urządzenia.
    /// </summary>
    
    public enum CommunicationCommands
    {
        ParametersInfo,
        GiveFood,
        GiveWatter,
        ResetHome,
    }
    public class Message
    {
        public CommunicationCommands Command { get; set; }
        public DateTime Now { get; set; }
    }
}