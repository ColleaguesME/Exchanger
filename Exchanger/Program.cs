using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Tesseract;
using ImageFormat = System.Drawing.Imaging.ImageFormat;

namespace TestConsole {
    class Program {

        internal enum MOUSEEVENTF : uint
        {
            Move = 0x01,
            LeftDown = 0x02,
            LeftUp = 0x04,
            RightDown = 0x08,
            RightUp = 0x10
        }

        [DllImport("user32.dll")]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hwnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, uint dwExtraInfo);


        static readonly Regex BuyMessageRegex = new Regex(@"\[\d{2}:\d{2}\] @From ([A-Za-zА-Яа-я]{3,23}): Hi. I'd like to buy your (\d+) ([a-z]+) for my (\d+) ([a-z]+) in Breach.");

        static List<Exchange> ExchangeList = new List<Exchange>();

        static Queue<Exchange> ExchangeQueue = new Queue<Exchange>();

        public enum ContentType
        {
            Stash,
            Inventory,
            TradeTop,
            TradeBot
        }


        static readonly Dictionary<int, Tuple<int[], int[]>> ResolutionToBounds = new Dictionary<int, Tuple<int[], int[]>> {
            { 1200 + (int)ContentType.Stash, new Tuple<int[], int[]>(
                new [] {19, 77, 136, 194, 253, 311, 370, 428, 487, 545, 604, 662, 721},
                new [] {180, 238, 297, 355, 414, 472, 531, 589, 648, 706, 765, 823, 882}
                )
            }, { 1200 + (int)ContentType.Inventory, new Tuple<int[], int[]>(
                new [] {1199, 1258, 1316, 1375, 1433, 1492, 1550, 1609, 1667, 1726, 1784, 1843, 1901},
                new [] {653, 712, 770, 829, 887, 946}
                )
            }, { 1200 + (int)ContentType.TradeTop, new Tuple<int[], int[]>(
                new [] {239, 298, 356, 415, 473, 532, 590, 649, 707, 766, 824, 883, 941},
                new [] {227, 285, 344, 402, 461, 519})
            }, { 1200 + (int)ContentType.TradeBot, new Tuple<int[], int[]>(
                new [] {239, 298, 356, 415, 473, 532, 590, 649, 707, 766, 824, 883, 941},
                new [] {594, 653, 711, 770, 828, 887}
                )
            },

            { 1080 + (int)ContentType.Stash, new Tuple<int[], int[]>(
                new [] {17, 70, 122, 175, 227, 280, 333, 385, 438, 491, 543, 596, 649},
                new [] {162, 214, 267, 320, 372, 425, 478, 530, 583, 636, 688, 741, 793}
                )
            }, { 1080 + (int)ContentType.Inventory, new Tuple<int[], int[]>(
                new [] {1272, 1324, 1377, 1430, 1482, 1535, 1588, 1640, 1693, 1746, 1798, 1851, 1904},
                new [] {588, 641, 693, 746, 799, 851}
                )
            }, { 1080 + (int)ContentType.TradeTop, new Tuple<int[], int[]>(
                new [] {311, 364, 417, 469, 522, 575, 627, 680, 733, 785, 838, 890, 943},
                new [] {204, 257, 309, 362, 415, 467}
                )
            }, { 1080 + (int)ContentType.TradeBot, new Tuple<int[], int[]>(
                new [] {311, 364, 417, 469, 522, 575, 627, 680, 733, 785, 838, 890, 943},
                new [] {535, 588, 640, 693, 746, 798}
                )
            },

            { 900 + (int)ContentType.Stash, new Tuple<int[], int[]>(
                new [] {14, 58, 102, 146, 190, 233, 277, 321, 365, 409, 453, 497, 541},
                new [] {135, 179, 222, 266, 310, 354, 399, 442, 486, 530, 573, 617, 661}
                )
            }, { 900 + (int)ContentType.Inventory, new Tuple<int[], int[]>(
                new [] {1060, 1104, 1148, 1191, 1235, 1279, 1323, 1367, 1411, 1455, 1499, 1542, 1586},
                new [] {490, 534, 578, 622, 665, 709}
                )
            },{ 900 + (int)ContentType.TradeTop, new Tuple<int[], int[]>(
                new [] {259, 303, 347, 391, 435, 479, 523, 567, 610, 654, 698, 742, 786},
                new [] {170, 214, 258, 302, 346, 389}
                )
            }, { 900 + (int)ContentType.TradeBot, new Tuple<int[], int[]>(
                new [] {259, 303, 347, 391, 435, 479, 523, 567, 610, 654, 698, 742, 786},
                new [] {446, 490, 533, 577, 621, 665}
                )
            }
        };

        public static Dictionary<int, Dictionary<ContentType, Tuple<string, string, int, int>>> ResolutionToCutOptionsDictionary = new Dictionary<int, Dictionary<ContentType, Tuple<string, string, int, int>>>()
        {
           { 900, new Dictionary<ContentType, Tuple<string, string, int, int>>
               {
                   {ContentType.Stash, new Tuple<string, string, int, int>("/stash" , "/orbs" , 0, 6 )},
                   {ContentType.TradeTop, new Tuple<string, string, int, int>("/receive", "/orbsReceive", 6, 4) }
               }
           },
            { 1080, new Dictionary<ContentType, Tuple<string, string, int, int>>
                {
                    {ContentType.Stash, new Tuple<string, string, int, int>("/stash", "/orbs" , 0, 5)},
                    {ContentType.TradeTop, new Tuple<string, string, int, int>("/receive", "/orbsReceive", 5, 5)}
                }
            },
            {1200, new Dictionary<ContentType, Tuple<string, string,  int, int>>
                {
                    {ContentType.Stash, new Tuple<string, string,  int, int>("/stash", "/orbs" , 0, 4)},
                    {ContentType.TradeTop, new Tuple<string, string, int, int>("/receive", "/orbsReceive", 4, 4)}
                }
            }

        };


        public static Dictionary<int, Tuple<Point, Point>> ResolutionsToBeginEndXy = new Dictionary
            <int, Tuple<Point, Point>>()
            {
                {1200, new Tuple<Point, Point>(new Point(17, 28), new Point(42, 44))},
                {1080, new Tuple<Point, Point>(new Point(20, 25), new Point(36, 36))},
                {900, new Tuple<Point, Point>(new Point(18, 22), new Point(30, 32))}
            };

        public static Dictionary<int, Tuple<Point, Dictionary<Color, string>>> ResolutionToUniqPixelsGeneral =
            new Dictionary<int, Tuple<Point, Dictionary<Color, string>>>();

        public static Dictionary<int, Tuple<Point, Dictionary<Color, string>>> ResolutionToUniqPixelsReceive =
            new Dictionary<int, Tuple<Point, Dictionary<Color, string>>>();


        public static List<string> OrbsNamesList = new List<string>() { "alchemy", "alteration", "chaos", "chromatic", "fusing", "jeweller's", "vaal" };

        public static string Path = @"D:/Exchanger/";

        public static List<int> ResolutionsList = new List<int>() { 900, 1080, 1200 };


        static void Main()
        {

            ExchangeList = GetMessages(@"D:/Exchanger/firstResult");
            var newExchangeList = GetMessages(@"D:/Exchanger/secondResult");
            ExchangeList = newExchangeList.Except(ExchangeList).ToList();
            var a = ExchangeList.Count;

            //CaptureScreen(@"D:\Exchanger\second.bmp");
            //Slow(new Bitmap(@"D:\Exchanger\second.bmp"), @"D:\Exchanger\secondToProcess.bmp");
            //ProcessImage(@"D:\Exchanger\secondToProcess.bmp", @"D:\Exchanger\secondResult");

        }
        
        static List<Exchange> GetMessages(string resource)
        {
            var firstResult = Encoding.UTF8.GetString(File.ReadAllBytes(resource));
            firstResult = firstResult.Replace("\n\n", " ").Replace("\n", " ").Replace(";", ":").Replace("your1", "your 1");
            var matches = BuyMessageRegex.Matches(firstResult);
            var result = new List<Exchange>();
            foreach (Match match in matches)
            {
                result.Add(new Exchange()
                {
                    Nickname = match.Groups[1].Value,
                    OfferedOrbName = match.Groups[5].Value,
                    OfferedOrbQuantity = Int32.Parse(match.Groups[4].Value),
                    RequestedOrbName = match.Groups[3].Value,
                    RequestedOrbQuantity = Int32.Parse(match.Groups[2].Value)
           
                });
            }
            return result;
        }

        static void Slow(Bitmap bmp, string output) {
            for (var x = 0; x < bmp.Width; x++) {
                for (var y = 0; y < bmp.Height; y++) {
                    var currentColor = bmp.GetPixel(x, y);
                    var k = (currentColor.R + currentColor.G + currentColor.B) / 3;
                    if (neutral.Any(color => color == currentColor)) {
                        var l = (255 - k)/151;
                        bmp.SetPixel(x, y, Color.FromArgb(l, l, l));
                    } 
                    else if (from.Any(color => color == currentColor)) {
                        var l = (156 - k)/96;
                        bmp.SetPixel(x, y, Color.FromArgb(l, l, l));
                    }
                    else if (to.Any(color => color == currentColor)) {
                        var l = (146 - k)/99;
                        bmp.SetPixel(x, y, Color.FromArgb(l, l, l));
                    }
                    else {
                        bmp.SetPixel(x, y, Color.White);
                    }
                }
            }

            bmp.Save(output, ImageFormat.Bmp);
        }

        static void SlowTest(Bitmap bmp, string output) {
            for (var x = 0; x < bmp.Width; x++) {
                for (var y = 0; y < bmp.Height; y++) {
                    var currentColor = bmp.GetPixel(x, y);
                    if (neutral.Any(color => color == currentColor)) {

                        bmp.SetPixel(x, y, Color.FromArgb(255 - currentColor.R, 255 - currentColor.G, 255 - currentColor.B));
                    } else if (from.Any(color => color == currentColor)) {
                        var k = (currentColor.R + currentColor.G + currentColor.B) / 3;
                        var l = (156 - k) * 121 / 80;
                        bmp.SetPixel(x, y, Color.FromArgb(l, l, l));
                    } else if (to.Any(color => color == currentColor)) {
                        var k = (currentColor.R + currentColor.G + currentColor.B) / 3;
                        var l = (146 - k) * 121 / 83;
                        bmp.SetPixel(x, y, Color.FromArgb(l, l, l));
                    }
                }
            }

            bmp.Save(output, ImageFormat.Bmp);
        }

        static void Fast(Bitmap bmp) {
            var bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, bmp.PixelFormat);

            // Get the address of the first line.
            var ptr = bmpData.Scan0;

            // Declare an array to hold the bytes of the bitmap.
            var bytes = Math.Abs(bmpData.Stride) * bmp.Height;
            var rgbValues = new byte[bytes];

            // Copy the RGB values into the array.
            Marshal.Copy(ptr, rgbValues, 0, bytes);

            // Set every third value to 255. A 24bpp bitmap will look red.  
            for (var counter = 0; counter < rgbValues.Length; counter += 3) {
                rgbValues[counter] = 255;
            }

            // Copy the RGB values back to the bitmap
            Marshal.Copy(rgbValues, 0, ptr, bytes);

            // Unlock the bits.
            bmp.UnlockBits(bmpData);
        }

        static void CaptureScreen(string output) {
            SetForegroundWindow(FindWindow(null, "Path of Exile"));

            SendKeys.SendWait("{Enter}");

            Thread.Sleep(2000);

            using (var bmpScreenCapture = new Bitmap(700, 380)) {
                using (var g = Graphics.FromImage(bmpScreenCapture)) {
                    g.CopyFromScreen(40,
                                     394,
                                     0, 0,
                                     bmpScreenCapture.Size,
                                     CopyPixelOperation.SourceCopy);
                }
                bmpScreenCapture.Save(output, ImageFormat.Bmp);
            }
            
        }

        static void ProcessImage(string testImagePath, string destinationFile) {

            //Console.WriteLine(Directory.GetCurrentDirectory());

            try {
                using (var engine = new TesseractEngine(@"C:\Users\Vasya\Source\Repos\Exchanger\Exchanger\tessdata", "eng", EngineMode.Default)) {
                    using (var img = Pix.LoadFromFile(testImagePath)) {
                        using (var page = engine.Process(img))
                        {
                            var text = page.GetText();
                            var result = new List<string>();
                            
                            var writer = new StreamWriter(destinationFile);
                            writer.Write(text);
                            writer.Close();
                            //Console.ReadKey();
                            //Console.WriteLine("Mean confidence: {0}", page.GetMeanConfidence());

                            //Console.WriteLine("Text (GetText): \r\n{0}", text);
                            //Console.WriteLine("Text (iterator):");
                            //using (var iter = page.GetIterator())
                            //{
                            //    iter.Begin();

                            //    do
                            //    {
                            //        do
                            //        {
                            //            do
                            //            {
                            //                do
                            //                {
                            //                    if (iter.IsAtBeginningOf(PageIteratorLevel.Block))
                            //                    {
                            //                        Console.WriteLine("<BLOCK>");
                            //                    }

                            //                    Console.Write(iter.GetText(PageIteratorLevel.Word));
                            //                    Console.Write(" ");
                            //                    if (iter.IsAtFinalOf(PageIteratorLevel.TextLine, PageIteratorLevel.Word))
                            //                    {
                                                    
                            //                        Console.WriteLine("TextLine Word");
                            //                    }
                            //                } while (iter.Next(PageIteratorLevel.TextLine, PageIteratorLevel.Word));

                            //                if (iter.IsAtFinalOf(PageIteratorLevel.Para, PageIteratorLevel.TextLine))
                            //                {
                            //                    result.Add(iter.GetText(PageIteratorLevel.Para));
                            //                    Console.WriteLine("Para TextLine");
                            //                }
                            //            } while (iter.Next(PageIteratorLevel.Para, PageIteratorLevel.TextLine));
                            //        } while (iter.Next(PageIteratorLevel.Block, PageIteratorLevel.Para));
                            //    } while (iter.Next(PageIteratorLevel.Block));
                            //}
  
                        }
                    }
                }
            } catch (Exception e) {
                Trace.TraceError(e.ToString());
                Console.WriteLine("Unexpected Error: " + e.Message);
                Console.WriteLine("Details: ");
                Console.WriteLine(e.ToString());
            }
        }

        static List<Color> neutral = new List<Color> {
            Color.FromArgb(254, 254, 254),
            Color.FromArgb(248, 248, 247),
            Color.FromArgb(248, 247, 247),
            Color.FromArgb(247, 247, 247),
            Color.FromArgb(239, 239, 239),
            Color.FromArgb(232, 232, 232),
            Color.FromArgb(224, 224, 224),
            Color.FromArgb(216, 216, 216),
            Color.FromArgb(207, 207, 207),
            Color.FromArgb(198, 198, 198),
            Color.FromArgb(188, 188, 188),
            Color.FromArgb(177, 177, 177),
            Color.FromArgb(166, 166, 166),
            Color.FromArgb(164, 164, 164),
            Color.FromArgb(153, 153, 153),
            Color.FromArgb(139, 139, 139),
            Color.FromArgb(123, 123, 123),
            Color.FromArgb(104, 104, 104),
            Color.FromArgb(104, 104, 103),
            Color.FromArgb(103, 103, 103)
        };

        static List<Color> from = new List<Color> {
            Color.FromArgb(156, 98, 214), //156
            Color.FromArgb(154, 97, 208),
            Color.FromArgb(154, 96, 208),
            Color.FromArgb(153, 96, 208),
            Color.FromArgb(153, 95, 208),
            Color.FromArgb(152, 95, 208),
            Color.FromArgb(147, 91, 201),
            Color.FromArgb(142, 89, 195),
            Color.FromArgb(137, 85, 188),
            Color.FromArgb(132, 82, 182),
            Color.FromArgb(126, 78, 174),
            Color.FromArgb(121, 74, 166),
            Color.FromArgb(114, 70, 158),
            Color.FromArgb(108, 66, 149),
            Color.FromArgb(101, 61, 139),
            Color.FromArgb(92, 56, 128),
            Color.FromArgb(83, 50, 116),
            Color.FromArgb(73, 43, 103), // 73
            Color.FromArgb(62, 38, 87),
            Color.FromArgb(62, 37, 87),
            Color.FromArgb(62, 36, 86),
            Color.FromArgb(61, 37, 87),
            Color.FromArgb(61, 35, 86) // 60
        };

        static List<Color> to = new List<Color>() {
            Color.FromArgb(131, 98, 150), //146
            Color.FromArgb(130, 98, 147),
            Color.FromArgb(130, 97, 147),
            Color.FromArgb(130, 97, 146),
            Color.FromArgb(129, 97, 146),
            Color.FromArgb(129, 96, 146),
            Color.FromArgb(128, 96, 146),
            Color.FromArgb(128, 95, 146),
            Color.FromArgb(127, 95, 146),
            Color.FromArgb(123, 91, 141),
            Color.FromArgb(119, 89, 137),
            Color.FromArgb(115, 85, 132),
            Color.FromArgb(111, 82, 127),
            Color.FromArgb(106, 78, 121),
            Color.FromArgb(101, 74, 116),
            Color.FromArgb(95, 70, 110),
            Color.FromArgb(90, 66, 103),
            Color.FromArgb(84, 61, 97),
            Color.FromArgb(77, 56, 88),
            Color.FromArgb(69, 50, 80), //66
            Color.FromArgb(60, 43, 70),
            Color.FromArgb(51, 37, 60),
            Color.FromArgb(50, 36, 59),
            Color.FromArgb(50, 35, 58) // 47
        };

        public static void TestingTupleOfDictionary(int resolution, string filenameOfTestingPicture, string filenameOfResultOfTest, Tuple<Point, Dictionary<Color, string>> testingTuple, ContentType type)
        {
            var testingPicture = new Bitmap(Path + resolution + "/" + filenameOfTestingPicture);
            var writer = new StreamWriter(Path + resolution + "/" + filenameOfResultOfTest);
            var bounds = ResolutionToBounds[resolution + (int)type];
            for (var j = 0; j < bounds.Item2.Length - 1; j++)
            {
                for (var i = 0; i < bounds.Item1.Length - 1; i++)
                {
                    var x = bounds.Item1[i] + testingTuple.Item1.X;
                    var y = bounds.Item2[j] + testingTuple.Item1.Y;
                    var color = testingPicture.GetPixel(x, y);

                    if (testingTuple.Item2.ContainsKey(color))
                    {
                        writer.Write(i + " " + j + " " + testingTuple.Item2[color] + "\n");
                    }
                }
            }
            writer.Close();

        }



        public static Tuple<Point, Dictionary<Color, string>> CreatingTupleForDictionary(int resolution, string filename)
        {
            var information = File.ReadAllText(Path + resolution + "/" + filename, Encoding.UTF8);
            var splitedInformation = information.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
            var point = splitedInformation[0].Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);
            var separator = (splitedInformation.Count - 1) / OrbsNamesList.Count;
            var colorToOrbName = new Dictionary<Color, string>();
            for (var i = 1; i < splitedInformation.Count; i++)
            {
                var colors = splitedInformation[i].Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                colorToOrbName[
                    Color.FromArgb(Convert.ToInt32(colors[0]), Convert.ToInt32(colors[1]), Convert.ToInt32(colors[2]))] = OrbsNamesList[(i - 1) / separator];
            }
            return new Tuple<Point, Dictionary<Color, string>>(new Point(Convert.ToInt32(point[0]), Convert.ToInt32(point[1])), colorToOrbName);
        }


        public static void GetIcons(int resolution, ContentType type)
        {
            var bounds = ResolutionToBounds[resolution + (int)type];
            var options = ResolutionToCutOptionsDictionary[resolution];
            var rows = bounds.Item2;
            var columns = bounds.Item1;
            var bitmaps = Directory.GetFiles(Path + resolution + options[type].Item1).Select(file => new Bitmap(file)).ToList();
            var information = File.ReadAllText(Path + resolution + "/" + resolution + "All.txt", Encoding.UTF8).Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
            var indexes = new List<int>();

            switch (resolution)
            {
                case 900:
                    {
                        for (var i = options[type].Item3; i < options[type].Item3 + options[type].Item4; i++)
                        {
                            var index = information[i].Split('-');
                            indexes.Add(type == ContentType.TradeTop ? Convert.ToInt32(index[0]) - 204 : Convert.ToInt32(index[0]));
                        }
                        for (var k = 0; k < bitmaps.Count; k++)
                        {
                            for (var j = 0; j < rows.Length - 1; j++)
                            {
                                for (var i = 0; i < columns.Length - 1; i++)
                                {
                                    if (!indexes.Contains(j * (columns.Length - 1) + i)) continue;
                                    var x = columns[i];
                                    var width = columns[i + 1] - x;
                                    var y = rows[j];
                                    var height = rows[j + 1] - y;
                                    var icon = bitmaps[k].Clone(new Rectangle(x, y, width, height), bitmaps[k].PixelFormat);
                                    icon.Save(Path + 900 + options[type].Item2 + "/" + OrbsNamesList[k] + (j * (columns.Length - 1) + i) + ".bmp", ImageFormat.Bmp);
                                }
                            }
                        }
                        break;
                    }
                case 1080:
                    {
                        for (var i = 0; i < (type == ContentType.Stash ? 7 : 4); i++)
                        {
                            for (var j = options[type].Item3; j < options[type].Item3 + options[type].Item4; j++)
                            {
                                var row = information[j].Split('-');
                                indexes.Add(type == ContentType.Stash ? Convert.ToInt32(row[i]) : Convert.ToInt32(row[i]) - 204);
                            }
                        }
                        for (var i = 0; i < indexes.Count; i++)
                        {
                            var x = columns[indexes[i] % 12];
                            var width = columns[indexes[i] % 12 + 1] - x;
                            var y = rows[indexes[i] / 12];
                            var height = rows[indexes[i] / 12 + 1] - y;
                            var icon = bitmaps[0].Clone(new Rectangle(x, y, width, height), bitmaps[0].PixelFormat);

                            icon.Save(Path + 1080 + options[type].Item2 + "/" + OrbsNamesList[i / options[type].Item4] + indexes[i] + ".bmp", ImageFormat.Bmp);
                        }
                        if (type == ContentType.TradeTop)
                        {
                            for (var i = 0; i < indexes.Count - options[type].Item4; i++)
                            {
                                var x = columns[indexes[i] % 12];
                                var width = columns[indexes[i] % 12 + 1] - x;
                                var y = rows[indexes[i] / 12];
                                var height = rows[indexes[i] / 12 + 1] - y;
                                var icon = bitmaps[1].Clone(new Rectangle(x, y, width, height), bitmaps[1].PixelFormat);

                                icon.Save(
                                    Path + 1080 + options[type].Item2 + "/" + OrbsNamesList[i / options[type].Item4 + 4] + indexes[i] +
                                    ".bmp", ImageFormat.Bmp);
                            }
                        }
                        break;
                    }
                case 1200:
                    {
                        for (var i = 0; i < 7; i++)
                        {
                            for (var j = options[type].Item3; j < options[type].Item3 + options[type].Item4; j++)
                            {
                                var row = information[j].Split('-');
                                indexes.Add(type == ContentType.Stash ? Convert.ToInt32(row[i]) : Convert.ToInt32(row[i]) - 204);
                            }
                        }
                        for (var i = 0; i < indexes.Count; i++)
                        {
                            var x = columns[indexes[i] % 12];
                            var width = columns[indexes[i] % 12 + 1] - x;
                            var y = rows[indexes[i] / 12];
                            var height = rows[indexes[i] / 12 + 1] - y;
                            var icon = new Bitmap(width, height);
                            Graphics.FromImage(icon)
                                .DrawImage(bitmaps[0], 0, 0, new Rectangle(x, y, width, height), GraphicsUnit.Pixel);

                            icon.Save(Path + 1200 + options[type].Item2 + "/" + OrbsNamesList[i / options[type].Item4] + indexes[i] + ".bmp", ImageFormat.Bmp);
                        }
                        break;
                    }
            }
        }



        public static List<Tuple<int, System.Drawing.Point>> GetUniqPixelsList(List<Tuple<int, System.Drawing.Point>> listToCompareWith, List<Bitmap> listOfBitmaps)
        {
            listOfBitmaps.ForEach(bitmap =>
            {

                System.Drawing.Rectangle rect = new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
                BitmapData bmpData1 = bitmap.LockBits(rect, ImageLockMode.ReadOnly, bitmap.PixelFormat);
                unsafe
                {
                    int* ptr = (int*)bmpData1.Scan0.ToPointer();
                    // for 32bpp pixel data

                    for (int y = 0; y < rect.Height; y++)
                    {
                        for (int x = 0; x < rect.Width; x++)
                        {
                            listToCompareWith.RemoveAll(
                                l => l.Item1 == *ptr);
                            ptr++;
                        }

                    }
                }
                bitmap.UnlockBits(bmpData1);
            });


            return listToCompareWith;
        }

        public static void WriteUniqPixelsForCertainArea(int resolution, string filename, string nameOfDirectoryWithBitmaps)
        {
            var bitmaps =
                Directory.GetFiles(Path + resolution + "/" + nameOfDirectoryWithBitmaps)
                    .Select(file => new Tuple<string, Bitmap>(OrbsNamesList.First(file.Contains), new Bitmap(file))).ToList();
            var dataToWrite = GetUniqPixelAndArrayOfColorsInCertainAreaOfPicture(ResolutionsToBeginEndXy[resolution].Item1, ResolutionsToBeginEndXy[resolution].Item2, bitmaps);
            var writer = new StreamWriter(Path + resolution + "/" + filename);
            writer.Write(dataToWrite.Item1.X + " " + dataToWrite.Item1.Y + "\n");
            foreach (var color in dataToWrite.Item2)
            {
                writer.Write(color.R + " " + color.G + " " + color.B + "\n");
            }
            writer.Close();
        }

        public static void WriteUniqPixels(int resolution, string filename, string nameOfDirectoryWithBitmaps)
        {
            var bitmaps =
                Directory.GetFiles(Path + resolution + "/" + nameOfDirectoryWithBitmaps)
                    .Select(file => new Tuple<string, Bitmap>(OrbsNamesList.First(file.Contains), new Bitmap(file))).ToList();
            var dataToWrite = GetUniqPixelAndArrayOfColors(resolution, bitmaps);
            var writer = new StreamWriter(Path + resolution + "/" + filename);
            writer.Write(dataToWrite.Item1.X + " " + dataToWrite.Item1.Y + "\n");
            foreach (var color in dataToWrite.Item2)
            {
                writer.Write(color.R + " " + color.G + " " + color.B + "\n");
            }
            writer.Close();
        }

        private static Tuple<Point, List<Color>> GetUniqPixelAndArrayOfColorsInCertainAreaOfPicture(Point leftUpCorner, Point rightDownCorner, List<Tuple<string, Bitmap>> listToAnalize)
        {
            for (var y = leftUpCorner.Y; y < rightDownCorner.Y; y++)
            {
                for (var x = leftUpCorner.X; x < rightDownCorner.X; x++)
                {
                    var listOfUniqColors = new List<Color>();
                    var listOfOrbColors = new Dictionary<string, List<Color>>();
                    foreach (var t in listToAnalize)
                    {
                        var color = t.Item2.GetPixel(x, y);
                        if (listOfUniqColors.Contains(color) && (listOfOrbColors.ContainsKey(t.Item1) && !listOfOrbColors[t.Item1].Contains(color) || !listOfOrbColors.ContainsKey(t.Item1)))
                        {
                            break;
                        }
                        listOfUniqColors.Add(color);
                        if (!listOfOrbColors.ContainsKey(t.Item1))
                        {
                            listOfOrbColors[t.Item1] = new List<Color>();
                        }
                        listOfOrbColors[t.Item1].Add(color);
                    }
                    if (listOfUniqColors.Count == listToAnalize.Count)
                    {
                        return new Tuple<Point, List<Color>>(new Point(x, y), listOfUniqColors);
                    }
                }
            }
            return null;
        }

        public static Tuple<Point, List<Color>> GetUniqPixelAndArrayOfColors(int resolution, List<Tuple<string, Bitmap>> listToAnalize)
        {
            var bounds = ResolutionToBounds[resolution + (int)ContentType.Stash];
            var height = bounds.Item2[1] - bounds.Item2[0] - 1;
            var width = bounds.Item1[1] - bounds.Item1[0] - 1;
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var listOfUniqColors = new List<Color>();
                    var listOfOrbColors = new Dictionary<string, List<Color>>();
                    foreach (var t in listToAnalize)
                    {
                        var color = t.Item2.GetPixel(x, y);
                        if (listOfUniqColors.Contains(color) && (listOfOrbColors.ContainsKey(t.Item1) && !listOfOrbColors[t.Item1].Contains(color) || !listOfOrbColors.ContainsKey(t.Item1)))
                        {
                            break;
                        }
                        listOfUniqColors.Add(color);
                        if (!listOfOrbColors.ContainsKey(t.Item1))
                        {
                            listOfOrbColors[t.Item1] = new List<Color>();
                        }
                        listOfOrbColors[t.Item1].Add(color);
                    }
                    if (listOfUniqColors.Count == listToAnalize.Count)
                    {
                        return new Tuple<Point, List<Color>>(new Point(x, y), listOfUniqColors);
                    }
                }
            }
            return null;
        }

        public static List<Tuple<int, System.Drawing.Point>> GetUniqPixelsList(Bitmap bmp1, Bitmap bmp2)
        {
            var listOfUniqPixels = new List<Tuple<int, System.Drawing.Point>>();

            System.Drawing.Rectangle rect = new System.Drawing.Rectangle(0, 0, /*bmp1.Width*/ 52, 52/*bmp1.Height*/);
            BitmapData bmpData1 = bmp1.LockBits(rect, ImageLockMode.ReadOnly, bmp1.PixelFormat);
            BitmapData bmpData2 = bmp2.LockBits(rect, ImageLockMode.ReadOnly, bmp2.PixelFormat);
            unsafe
            {
                var ptr1 = (int*)bmpData1.Scan0.ToPointer();
                var ptr2 = (int*)bmpData2.Scan0.ToPointer();
                // for 32bpp pixel data
                for (int y = 0; y < rect.Height; y++)
                {
                    for (int x = 0; x < rect.Width; x++)
                    {
                        if (*ptr1 != *ptr2)
                        {
                            listOfUniqPixels.Add(new Tuple<int, Point>(*ptr1, new Point(x, y)));
                        }
                        ptr1++;
                        ptr2++;
                    }
                }
            }
            bmp1.UnlockBits(bmpData1);
            bmp2.UnlockBits(bmpData2);
            return listOfUniqPixels;
        }


        public static bool ComparePictures(Bitmap bmp1, Bitmap bmp2)
        {
            System.Drawing.Rectangle rect = new System.Drawing.Rectangle(0, 0, bmp1.Width, bmp1.Height);
            BitmapData bmpData1 = bmp1.LockBits(rect, ImageLockMode.ReadOnly, bmp1.PixelFormat);
            BitmapData bmpData2 = bmp2.LockBits(rect, ImageLockMode.ReadOnly, bmp2.PixelFormat);
            unsafe
            {
                byte* ptr1 = (byte*)bmpData1.Scan0.ToPointer();
                byte* ptr2 = (byte*)bmpData2.Scan0.ToPointer();
                int width = rect.Width * 3; // for 24bpp pixel data
                for (int y = 0; y < rect.Height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (*ptr1 != *ptr2)
                        {
                            return false;

                        }
                        ptr1++;
                        ptr2++;
                    }
                    ptr1 += bmpData1.Stride - width;
                    ptr2 += bmpData2.Stride - width;
                }
            }
            bmp1.UnlockBits(bmpData1);
            bmp2.UnlockBits(bmpData2);
            return true;
        }
    }
}
