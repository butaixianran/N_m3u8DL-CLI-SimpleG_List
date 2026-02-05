using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace N_m3u8DL_CLI_SimpleG
{
    [Serializable]
    public class M3u8TaskItem
    {
        public string Name { get; set; }
        public string M3u8Url { get; set; }
        public string PageUrl { get; set; }
        public string Parameter { get; set; }
        public string Status { get; set; }

        public M3u8TaskItem(string name, string m3u8Url, string pageUrl, string parameter)
        {
            this.Name = name;
            this.M3u8Url = m3u8Url;
            this.PageUrl = pageUrl;
            this.Parameter = parameter;
            this.Status = "";
        }
    }
}
