
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;

namespace ProxyEasyWithThreads
{
    class Program
    {
        static void Main(string[] args)
        {
            var ipLocal = "127.0.0.1";
            var portLocal = 8888;
            TcpListener TCP = new TcpListener(IPAddress.Parse(ipLocal), portLocal);
            // поехали!
            WriteLog($"Запуск сервера {ipLocal}:{portLocal}");
            TCP.Start();
            WriteLog($"Сервер запущен. Ожидание запросов.");

            while (true)
            {
                // смотрим, есть запрос или нет
                if (TCP.Pending())
                {
                    WriteLog($"Получен запрос");
                    // запрос есть
                    // передаем его в отдельный поток
                    Thread thread = new Thread(ExecuteRequest);
                    thread.IsBackground = true;
                    thread.Start(TCP.AcceptSocket());
                }
            }

            TCP.Stop();
        }

        private static void ExecuteRequest(object arg)
        {
            Socket client = (Socket)arg;
            // соединяемся
            try
            {
                if (client.Connected)
                {
                    
                    // получаем тело запроса
                    byte[] httpRequest = ReadToEnd(client);
                    
                    WriteLog($"Обработка {httpRequest.Length} байт");
                    
                    // ищем хост и порт
                    Regex req = new Regex(@"Host: (((?<host>.+?):(?<port>\d+?))|(?<host>.+?))\s+", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                    Match match = req.Match(System.Text.Encoding.ASCII.GetString(httpRequest));                    
                    string host = match.Groups["host"].Value;
                    
                    int port = 0;
                    // если порта нет, то используем 80 по умолчанию
                    if (!int.TryParse(match.Groups["port"].Value, out port)) { port = 80; }

                    // получаем ip по хосту
                    IPHostEntry IPHostEntry = Dns.GetHostEntry(host);

                    // создаем точку доступа
                    IPEndPoint IPEndPoint = new IPEndPoint(IPHostEntry.AddressList[0], port);
                    WriteLog("Запрос от: " + host + ":" + port);
                    // создаем сокет и передаем ему запрос
                    using (Socket Rerouting = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                    {
                        Rerouting.Connect(IPEndPoint);
                        if (Rerouting.Send(httpRequest, httpRequest.Length, SocketFlags.None) != httpRequest.Length)
                        {
                            WriteLog("При отправке данных удаленному серверу произошла ошибка...");
                        }
                        else
                        {
                            // получаем ответ
                            byte[] httpResponse = ReadToEnd(Rerouting);
                            // передаем ответ обратно клиенту
                            if (httpResponse != null && httpResponse.Length > 0)
                            {
                                WriteLog("Ответ получен");
                                client.Send(httpResponse, httpResponse.Length, SocketFlags.None);
                            }
                        }
                    }
                    client.Close();
                }
            }
            catch
            {
                WriteLog("Запрос не может быть удовлетворен, т.к. протокол HTTP не реализован.");
            }
        }

        private static byte[] ReadToEnd(Socket Socket)
        {
            byte[] result = new byte[Socket.ReceiveBufferSize];
            int lenght = 0;
            try
            {
                using (MemoryStream memory = new MemoryStream())
                {
                    while (Socket.Poll(1000000, SelectMode.SelectRead) && (lenght = Socket.Receive(result, Socket.ReceiveBufferSize, SocketFlags.None)) > 0)
                    {
                        memory.Write(result, 0, lenght);
                    }
                    return memory.ToArray();
                }
            }
            catch
            {

            }
            return null;
        }

        private static void WriteLog(string msg, params object[] args)
        {
            Console.WriteLine(DateTime.Now.ToString() + " : " + msg, args);
        }
    }
}
