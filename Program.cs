using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.IO;

namespace McMotd
{
    class Program
    {
        static void Main(string[] args)
        {
            var config = ReadConfig();
            string ip = string.Empty;
            string _port =" 19132";
            HttpListener listener = new HttpListener();
            string address = "http://+:" + config.Port + config.Prefix;
            listener.Prefixes.Add(address);
            listener.Start();
            Console.WriteLine("[INFO] 服务器已开启:"+address);
            while (true)
            {
                HttpListenerContext context = listener.GetContext();
                Task.Run(() =>
                {
                    ip = context.Request.QueryString["ip"] ?? string.Empty;
                    _port = context.Request.QueryString["port"] ?? "19132";
                    int port = 19132;
                    HttpListenerRequest request = context.Request;
                    HttpListenerResponse response = context.Response;
                    response.ContentType = "application/json;charset=UTF-8";
                    response.ContentEncoding = Encoding.UTF8;
                    try
                    {
                        port = Convert.ToInt32(_port);
                    }
                    catch { port = 19132; };
                    string responseString = MotdPe(ip, port) ?? "{}";
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                    response.ContentLength64 = buffer.Length;
                    System.IO.Stream output = response.OutputStream;
                    output.Write(buffer, 0, buffer.Length);
                    response.StatusCode = 200;
                    output.Close();
                    Console.WriteLine("[{0} {1} INFO]IP:{2}请求查询{3}:{4}", DateTime.Now.ToLongDateString(), DateTime.Now.ToLongTimeString(), request.RemoteEndPoint.Address.ToString(), ip, port);
                });
            }
        }
        static Config ReadConfig() 
        {
            string path = "./Config/MotdPe.json";
            if (File.Exists(path))
            {
                Console.WriteLine("[INFO] 正在读取配置文件...");
                return JsonConvert.DeserializeObject<Config>(File.ReadAllText(path));
            }
            else
            {
                Console.WriteLine("[INFO] 正在生成配置文件...");
                JObject json = new JObject();
                json.Add(new JProperty("Prefix", "/MotdPe/"));
                json.Add(new JProperty("Port", "23333"));
                Directory.CreateDirectory("Config");
                File.WriteAllText(path, json.ToString());
                return new Config
                {
                    Prefix = "/MotdPe/",
                    Port = "23333"
                };
            }
        }
        static string MotdPe(string ip, int port=19132)
        {
            if (port > 65535) 
            {
                port = port % 65535;
            }
            var json = new JObject();
            try
            {
                Regex rx = new Regex(@"((?:(?:25[0-5]|2[0-4]\d|((1\d{2})|([1-9]?\d)))\.){3}(?:25[0-5]|2[0-4]\d|((1\d{2})|([1-9]?\d))))");
                if (!rx.IsMatch(ip))
                {
                    IPAddress[] IPs = Dns.GetHostAddresses(ip);
                    ip = IPs[0].ToString();
                }
                byte[] buffer = new byte[1024 * 1024 * 2];
                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.Connect(new IPEndPoint(IPAddress.Parse(ip), port));
                var msg = new List<byte>() { 0x00, 0xFF, 0xFF, 0x00, 0xFE, 0xFE, 0xFE, 0xFE, 0xFD, 0xFD, 0xFD, 0xFD, 0x12, 0x34, 0x56, 0x78, };
                var time = Encoding.UTF8.GetBytes(Convert.ToInt32(((DateTime.Now - DateTime.Parse("1970-1-1")).TotalSeconds)).ToString(), 0, 8);
                foreach (var i in time)
                {
                    msg.Insert(0, i);
                }
                msg.Insert(0, 0x01);
                socket.Send(msg.ToArray());
                int length = socket.Receive(buffer);
                string r = Encoding.UTF8.GetString(buffer, 0, length);
                var res = (from i in r.Split(";")
                           select i).ToList();
                res.RemoveAt(0);
                res.RemoveAt(5);
                json.Add(new JProperty("motd", res[0]));
                json.Add(new JProperty("protocolVersion", res[1]));
                json.Add(new JProperty("version", res[2]));
                json.Add(new JProperty("playerCount", res[3]));
                json.Add(new JProperty("maximumPlayerCount", res[4]));
                json.Add(new JProperty("subMotd", res[5]));
                json.Add(new JProperty("gameType", res[6]));
                json.Add(new JProperty("nintendoLimited", res[7]));
                json.Add(new JProperty("ipv4Port", res[8]));
                json.Add(new JProperty("ipv6Port", res[9]));
                json.Add(new JProperty("rawText", r));
            }
            catch
            {
                return null;
            }
            return json.ToString();
        }
    }
    class Config 
    {
        public string Prefix { get; set; }
        public string Port { get; set; }
    }
}
