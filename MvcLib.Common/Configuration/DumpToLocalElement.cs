using System.Configuration;

namespace MvcLib.Common.Configuration
{
    public class DumpToLocalElement : BooleanElementBase
    {
        [ConfigurationProperty("folder", DefaultValue = "~/App_Code")]
        public string Folder
        {
            get { return (string)this["folder"]; }
            set { this["folder"] = value; }
        }
    }
}