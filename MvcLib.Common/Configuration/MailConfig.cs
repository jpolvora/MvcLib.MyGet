using System.Configuration;

namespace MvcLib.Common.Configuration
{
    public class MailConfig : ConfigurationElement
    {
        [ConfigurationProperty("admin")]
        public string MailAdmin
        {
            get { return (string)this["admin"]; }
            set { this["admin"] = value; }
        }

        [ConfigurationProperty("developer")]
        public string MailDeveloper
        {
            get { return (string)this["developer"]; }
            set { this["developer"] = value; }
        }
    }
}