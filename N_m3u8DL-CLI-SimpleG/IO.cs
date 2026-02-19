using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Newtonsoft.Json;

namespace N_m3u8DL_CLI_SimpleG_List
{
    public class IO
    {
        // 文件名（相对于当前目录）
        public string FileName { get; set; }

        public IO(string fileName)
        {
            this.FileName = fileName + ".json";
        }


        // 使用 BinaryFormatter 将列表序列化为二进制文件（相对于当前目录）。
        public void Save(List<M3u8TaskItem> items)
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), FileName);
            try
            {
                // 将对象转换为格式化的 JSON 字符串
                string json = JsonConvert.SerializeObject(items, Formatting.Indented);

                // 写入文件（UTF-8 编码）
                File.WriteAllText(path, json, Encoding.UTF8);

            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("保存失败：" + ex.Message);
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
                // 读取全部文本
                string json = File.ReadAllText(path, Encoding.UTF8);

                // 反序列化
                var obj = JsonConvert.DeserializeObject<List<M3u8TaskItem>>(json);

                if (obj == null)
                {
                    System.Windows.MessageBox.Show("读取失败");
                    // 如果反序列化结果为 null，返回空列表
                    return new List<M3u8TaskItem>();
                }

                return obj;
            }
            catch(Exception ex) 
            {
                //弹窗显示出错消息
                System.Windows.MessageBox.Show("加载任务列表失败，可能是程序集版本变化。\n错误信息：" + ex.Message, "错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);

                // 如果反序列化失败，则返回空列表
                return new List<M3u8TaskItem>();
            }
        }


    }
}
