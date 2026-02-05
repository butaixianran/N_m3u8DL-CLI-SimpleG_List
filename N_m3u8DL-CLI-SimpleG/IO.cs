using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace N_m3u8DL_CLI_SimpleG
{
    public class IO
    {
        // 文件名（相对于当前目录）
        public string FileName { get; set; }

        public IO(string fileName)
        {
            this.FileName = fileName + ".data";
        }


        // 使用 BinaryFormatter 将列表序列化为二进制文件（相对于当前目录）。
        public void Save(List<M3u8TaskItem> items)
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), FileName);
            try
            {
                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var bf = new BinaryFormatter();
                    bf.Serialize(fs, items);
                }
            }
            catch
            {
                // 忽略序列化错误以保持与 JSON 方法一致的行为
            }
        }

        // 读取二进制文件并反序列化为列表。文件丢失或发生错误时返回空列表。
        public List<M3u8TaskItem> Load()
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), FileName);
            if (!File.Exists(path))
                return new List<M3u8TaskItem>();

            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var bf = new BinaryFormatter();
                    var obj = bf.Deserialize(fs) as List<M3u8TaskItem>;
                    return obj ?? new List<M3u8TaskItem>();
                }
            }
            catch
            {
                // 如果反序列化失败，则返回空列表
                return new List<M3u8TaskItem>();
            }
        }


    }
}
