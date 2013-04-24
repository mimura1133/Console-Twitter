using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;

namespace Console_Twitter
{
    /// <summary>
    /// ボスが来た！
    /// </summary>
    class FakeBoss
    {
        /// <summary>
        /// 偽装画面表示
        /// </summary>
        public static void init()
        {
            Console.Clear();
            Console.WriteLine();
            for (int i = 1; i < Console.WindowHeight - 2; i++)
                Console.WriteLine("~");
            Console.Write("/tmp/vi." + Path.GetRandomFileName() + ": new file: line 1");

            Thread.Sleep(3000);
            Console.SetCursorPosition(0, Console.CursorTop);
            Console.Write("                                            ");

            Console.SetCursorPosition(0, 0);

            List<int> linelength = new List<int>(1);
            linelength.Add(0);

            while (true)
            {
                var cki = Console.ReadKey(true);
                if (cki.KeyChar == ':')
                {
                    int x, y;

                    Console.Write("\b ");
                    x = Console.CursorLeft - 1;
                    y = Console.CursorTop;

                    Console.SetCursorPosition(0, Console.WindowHeight - 2);
                    Console.Write(":");

                    switch (Console.ReadLine())
                    {
                        case "unboss":
                            return;

                        case "q":
                            Console.Clear();
                            Environment.Exit(0);
                            break;

                        default:
                            Console.SetCursorPosition(0, Console.CursorTop - 1);
                            Console.Write("                                            ");
                            Console.SetCursorPosition(x, y);
                            break;
                    }
                }
                else
                {
                    switch (cki.KeyChar)
                    {
                        case '\r':
                            Console.WriteLine();
                            if (Console.CursorTop >= linelength.Count)
                            {
                                linelength.AddRange(Enumerable.Range(0, linelength.Count - Console.CursorTop + 1).Select(v => { return 0; }));
                                Console.Write(" \b");
                            }
                            
                            break;

                        case '\b':
                            Console.Write("\b \b");
                            if (Console.CursorLeft > 0)
                                linelength[Console.CursorTop]--;

                            if (Console.CursorLeft == 0 && Console.CursorTop > 0)
                                Console.SetCursorPosition(linelength[Console.CursorTop - 1], Console.CursorTop - 1);
                            break;

                        default:
                            linelength[Console.CursorTop]++;
                            Console.Write(cki.KeyChar);
                            break;
                    }
                }
            }
        }

    }
}
