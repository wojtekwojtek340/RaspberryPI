using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IotHubCommunication.CommunicationProvider.Message
{
    /// <summary>
    /// taka klasa testowa do obsługi odczytu danych wysyałanych do urządzenia.
    /// </summary>
    public class User
    {
        [JsonProperty("count")]
        public int Count { get; set; }

        [JsonProperty("Now")]
        public DateTime Now { get; set; }
    }
}