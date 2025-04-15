using System.Diagnostics;
using System.Text.Json;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NAudio.Wave;
using System.Windows.Media;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp;
using System.Windows.Documents;

namespace barhelper
{

    public static class ImageProcessingHelper // ИИ писал, скилл ишуй
    {
        public static CroppedBitmap ColorizeGrayscaleImage(CroppedBitmap croppedBitmap, string tintHex)
        {
            // Конвертируем HEX в RGB.
            Rgba32 tint = ColorFromHex(tintHex);

            // Кодируем CroppedBitmap в поток (PNG).
            using MemoryStream ms = new();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(croppedBitmap));
            encoder.Save(ms);
            ms.Position = 0;

            using Image<Rgba32> image = SixLabors.ImageSharp.Image.Load<Rgba32>(ms);
            // Проходим по каждому пикселю изображения.
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    Span<Rgba32> row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        Rgba32 original = row[x];

                        // Яркость рассчитывается на основе оттенков серого (R = G = B).
                        float intensity = original.R / 255f;

                        // Учитываем прозрачность пикселя.
                        float alphaFactor = original.A / 255f;

                        // Если прозрачность равна 0, пиксель пропускаем.
                        if (alphaFactor == 0)
                            continue;

                        // Генерируем новый пиксель с учетом яркости и прозрачности.
                        row[x] = new Rgba32(
                            (byte)(original.R * (1 - alphaFactor) + tint.R * intensity * alphaFactor),
                            (byte)(original.G * (1 - alphaFactor) + tint.G * intensity * alphaFactor),
                            (byte)(original.B * (1 - alphaFactor) + tint.B * intensity * alphaFactor),
                            original.A // Сохраняем прозрачность.
                        );
                    }
                }
            });

            // Преобразуем результат в BitmapSource.
            BitmapImage resultBitmapSource = ConvertImageSharpToBitmapSource(image);

            // Создаём CroppedBitmap для возвращения результата.
            CroppedBitmap result = new(resultBitmapSource, new Int32Rect(0, 0, resultBitmapSource.PixelWidth, resultBitmapSource.PixelHeight));
            result.Freeze();
            return result;
        }

        private static Rgba32 ColorFromHex(string hex)
        {
            hex = hex.TrimStart('#');

            if (hex.Length == 6)
            {
                byte r = byte.Parse(hex[..2], NumberStyles.HexNumber);
                byte g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                byte b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                return new Rgba32(r, g, b, 255);
            }
            else if (hex.Length == 8)
            {
                byte a = byte.Parse(hex[..2], NumberStyles.HexNumber);
                byte r = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
                byte g = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
                byte b = byte.Parse(hex.Substring(6, 2), NumberStyles.HexNumber);
                return new Rgba32(r, g, b, a);
            }
            else
            {
                throw new ArgumentException("HEX‑строка должна быть в формате #RRGGBB или #AARRGGBB", nameof(hex));
            }
        }
        private static BitmapImage ConvertImageSharpToBitmapSource(Image<Rgba32> image)
        {
            using MemoryStream ms = new();
            image.Save(ms, new PngEncoder());
            ms.Position = 0;

            BitmapImage bitmapImage = new();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = ms;
            bitmapImage.EndInit();
            bitmapImage.Freeze();
            return bitmapImage;
        }

    }
    public class AudioManager
    {
        public static void PlaySound(string filePath)
        {
            var waveOut = new WaveOutEvent();
            var audioFile = new WaveFileReader(Application.GetResourceStream(new Uri("pack://application:,,,/barhelper;component/Sounds/" + filePath)).Stream);
            waveOut.Init(audioFile);
            waveOut.Play();

            waveOut.PlaybackStopped += (sender, args) =>
            {
                waveOut.Dispose();
                audioFile.Dispose();
            };
        }
        public static void PlaySound(string filePath, Action onEnd)
        {
            var waveOut = new WaveOutEvent();
            var audioFile = new WaveFileReader(Application.GetResourceStream(new Uri("pack://application:,,,/barhelper;component/Sounds/" + filePath)).Stream);
            waveOut.Init(audioFile);
            waveOut.Play();

            waveOut.PlaybackStopped += (sender, args) =>
            {
                onEnd.Invoke();
                waveOut.Dispose();
                audioFile.Dispose();
            };
        }
    }

    public class History(Drink reagentobj)
    {
        public Drink drink = reagentobj;
        public Drink parentHistory = new();
        public double lastAmount;
        public float currentAmount;
    }
    public class Drink
    {
        public string flavor = "nothing";
        public string physicalDesc = "";
        public string name = "";
        public float SatiateThirst = 3f;
        public float Poison = 0;
        public string sprite = "glass_clear.rsi/";
        public bool changeColor = true;
        public bool hyperlinkson = false;
        public string color = "#00000000";
        public string id = "UNKNOWN";
        public float Ethanol = 0f;
        public string state = "glass";
        public Dictionary<string, int> maxLevels = new() { ["glass"] = 9 };
        public int index;
        public Dictionary<string, bool> openState = new() { ["glass"] = false };
        public Dictionary<string, int> fill = [];
        public Dictionary<string,Dictionary<string, State>> statesCache = [];
        public Dictionary<string, Dictionary<string, Dictionary<short, CroppedBitmap>>> states = [];

        public CroppedBitmap GetSprite(string name, short num= 0)
        {
            return states[state][name][num];
        }
        public State GetJSON(string name)
        {
            return statesCache[state][name];
        }
    }
    public class Recipe
    {
        public Dictionary<string, short> input = [];
        public string id = "";
        public short output = 0;
        public string action = "None";
    }
#pragma warning disable CS8618

    public class metaRSIJSON
    {
        public int version { get; set; }
        public Size size { get; set; }
        public string license { get; set; }
        public string copyright { get; set; }
        public State[] states { get; set; }
    }

    public class Size
    {
        public int x { get; set; }
        public int y { get; set; }
    }

    public class State
    {
        public string name { get; set; }
        public float[][] delays { get; set; }
    }
#pragma warning restore CS8618


    public partial class MainWindow : Window
    {
        [GeneratedRegex(@"^1(\.\d*)?$|^0(\.\d*)?$|^\.\d+$")]
        private partial Regex MyRegex();
        private float amount;
        private int selected = -1;
        private int selectedWas = -1;
        private readonly List<Drink> allDrinks = [];
        private readonly static List<string> byethanolblacklist = ["Cola", "SodaWater", "Tea", "Ethanol","LemonLime", "HotCocoa"];
        private Drink currentDrinkSelected = new();
        private readonly Dictionary<string, Drink> allIdsDrinks = [];
        private Dictionary<Drink, float> drinksHistory = [];
        private readonly static string errSound = "prompt.wav";
        private readonly static string yeahSound = "loghide.wav";
        private readonly static string welcomeSound = "click_to_start.wav";
        private readonly static string hoverSound = "mouse_hover.wav";
        private readonly static string clickFilterButtonSound = "jukepick.wav";
        private readonly static string clickSubFilterButtonSound = "jukechange.wav";
        private readonly static string openbottle = "bottle_open1.wav";
        private readonly static string closebottle = "bottle_close1.wav";
        private Recipe currentRecipe = new();
        private readonly static List<string> blacklist = ["Aloe","DrinkCartonVisualsOpenable", "DrinkCartonBaseLargeFull", "DrinkCartonBaseFull", "DrinkCanBaseFull", "DryRamen", "HotRamen", "UNKNOWN", "DrinkBottlePlasticBaseFull", "DrinkBottleGlassBaseFull", "DrinkBottleVisualsOpenable", "DrinkBottleVisualsAll", ""];
        private readonly static List<string> parentblacklist = ["DrinkBottlePlasticBaseFull"];
        private readonly static List<string> suffixblacklist = ["XL", "Pack", "Empty", "Growler"];
        private List<string> pathblacklist = [];
        private readonly static string pattern = @":\s*(.+)";
        private readonly SortedDictionary<string, List<Drink>> bynamedict = [];
        private readonly SortedDictionary<string, List<Drink>> byethanoldict = [];
        private readonly SortedDictionary<string, List<Drink>> byflavordict = [];
        private readonly Dictionary<string, string> palette = [];
        private readonly Dictionary<Drink, Recipe> recipe = [];
        private readonly List<string> recipeunbuilded = [];
        private readonly static FontFamily font = new(new Uri("pack://application:,,,/"), "./Fonts/#Classic Console Neue");
        private readonly Style filterbuttonstyle;
        private string currentDrinks = "";
        private Dictionary<int, List<string>> namesLists = [];
        private Dictionary<int, List<string>> namesListsAdditional = [];
        private Dictionary<int, List<List<string>>> fileLists = [];
        private Dictionary<int, List<List<string>>> fileListsAdditional = [];
        private readonly Dictionary<string, string> localization = [];
        private readonly List<string> loaded = [];
        private readonly Dictionary<string, string> flavors = [];
        private readonly Grid secondGrid = new();
        private DispatcherTimer drinksSpriteTime = new() { Interval = TimeSpan.FromHours(42)};
        private short filtersection = 0;
        private readonly static Random rand = new();
        private readonly static ResourceDictionary resourceDictionary = new()
        {
            Source = new Uri("Styles/FilterButtonStyle.xaml", UriKind.Relative)
        };
        private readonly BitmapImage arrowleftimage = new();
        private readonly BitmapImage arrowrightimage = new();
        private readonly BitmapImage arrowhistoryimage = new();
        private readonly BitmapImage morty = new();
        private readonly BitmapImage circle = new();
        private readonly BitmapImage jokerge = new();
        private readonly BitmapImage iconFront;

        private List<Drink> GetCurrentDrinks(string name)
        {
            return currentDrinks switch
            {
                "byname" => bynamedict[name],
                "byethanol" => byethanoldict[name],
                "byflavor" => byflavordict[name],
                _ => [],
            };
        }
        private string GetColor(string name)
        {
            if (palette.TryGetValue(name, out string? value))
            {
                return value;
            }
            return "KeyNotFound";
        }

        private string GetName(Drink wanted)
        {
            if (localization.TryGetValue(wanted.name, out string? value))
            {
                return CapitalizeWords(value);
            }
            return "KeyNotFound";
        }
        private string GetFlavor(Drink wanted, bool removeidkfuckyou = false)
        {
            if (localization.TryGetValue(flavors[wanted.flavor], out string? result))
            {
                if (!removeidkfuckyou)
                {
                    result = result.Replace("как ", "");
                }
                return result;
            }
            return "KeyNotFound";
        }
        private void SetFlavor(Drink wanted, string name, string ifnot = "")
        {
            if (flavors.ContainsKey(name))
            {
                if (localization.ContainsKey(flavors[name]))
                {
                    wanted.flavor = name;
                }
            }
            else
            {
                wanted.flavor = ifnot;
            }
        }
        private string GetDesc(Drink wanted)
        {
            if (localization.ContainsKey(wanted.name.Replace("name", "desc")))
            {
                return localization[wanted.name.Replace("name", "desc")];
            }
            return "KeyNotFound";
        }
        private string GetPthysDesc(Drink wanted)
        {
            if (localization.TryGetValue(wanted.physicalDesc, out string? value))
            {
                return value;
            }
            return "KeyNotFound";
        }
        private void LoadLocalization(string locPath)
        {
            string[] content = File.ReadAllLines(locPath);
            foreach (string oldline in content)
            {
                string line = Modstring(oldline);
                if (line.Contains('='))
                {
                    string[] parts = line.Split(" = ", StringSplitOptions.None);
                    if (localization.ContainsKey(parts[0]))
                    {
                        localization[parts[0]] = parts[1];
                    }
                    else
                        localization.Add(parts[0], parts[1]);
                }
            }
        }
        private static string Modstring(string needmod)
        {
            if (needmod.Contains('#') && !needmod.Contains("color: "))
            {
                return needmod.Split('#')[0];
            }
            else
            {
                return needmod;
            }
        }
        private async Task<bool> SpriteLoadAndValidate(Drink currentDrink)
        {
            bool spritesTestPass = true;
            currentDrink.fill.Add("glass", currentDrink.maxLevels["glass"]);
            await LoadRSIAsync(currentDrink, currentDrink.sprite, fileLists[currentDrink.maxLevels["glass"]],"glass", namesLists[currentDrink.maxLevels["glass"]]);
            if (!currentDrink.states["glass"].ContainsKey("fill1"))
                spritesTestPass = false;
            else if (!currentDrink.states["glass"].ContainsKey("icon"))
                spritesTestPass = false;
            return spritesTestPass;
        }

        private async Task LoadAdditionalContainers(string path)
        {
            string currentthing = path.Replace("./Prototypes/drinks", "").Replace(".yml", "").Replace("./Prototypes/Corvax/drinks", "");
            currentthing = currentthing[1..];
            string[] content = File.ReadAllLines(path);
            bool skipFirst = false;
            string id = "";
            string ourID = "";
            string parent = "";
            string sprite = "(mdr)grapejuice.rsi/";
            int maxLevel = 5;
            foreach (string oldline in content)
            {
                string line = Modstring(oldline);
                if (line.Contains("- type: entity"))
                {
                    if (id != "" && sprite != "(mdr)grapejuice.rsi/" && !blacklist.Contains(ourID) && skipFirst && !parentblacklist.Contains(parent))
                    {
                        bool failure = false;
                        foreach (string idk in suffixblacklist)
                        {
                            if (ourID.Contains(idk))
                            {
                                failure = true;
                            }
                        }
                        if (!failure)
                        {
                            bool was = allIdsDrinks[id].changeColor;
                            Drink drink = allIdsDrinks[id];
                            drink.changeColor = true;
                            LoadRSI(drink, sprite, fileListsAdditional[maxLevel], currentthing, namesListsAdditional[maxLevel]);
                            drink.changeColor = was;
                            if (!drink.states[currentthing].ContainsKey("fill1"))
                            {
                                maxLevel = 0;
                            }
                            drink.fill.Add(currentthing, maxLevel);
                            drink.openState.Add(currentthing, true);
                            drink.maxLevels[currentthing] = maxLevel;
                        }
                    }
                    id = "";
                    ourID = "";
                    parent = "";
                    sprite = "(mdr)grapejuice.rsi/";
                    maxLevel = 5;
                    skipFirst = true;
                }
                else if (line.Contains("sprite:") && sprite == "(mdr)grapejuice.rsi/")
                {
                    sprite = Regex.Match(line, pattern).Groups[1].Value.Replace("Objects/Consumable/Drinks/", "") + "/";
                }
                else if (line.Contains("ReagentId: "))
                {
                    id = Regex.Match(line, pattern).Groups[1].Value.Trim();
                }
                else if (line.Contains("id: ") && id == "")
                {
                    ourID = Regex.Match(line, pattern).Groups[1].Value.Trim();
                }
                else if (line.Contains("parent: "))
                {
                    parent = Regex.Match(line, pattern).Groups[1].Value.Trim();
                }
                else if (line.Contains("maxFillLevels"))
                {
                    maxLevel = int.Parse(Regex.Match(line, pattern).Groups[1].Value);
                }
            }
            if (id != "" && sprite != "(mdr)grapejuice.rsi/" && !blacklist.Contains(ourID) && skipFirst && !parentblacklist.Contains(parent))
            {
                bool failure = false;
                foreach (string idk in suffixblacklist)
                {
                    if (ourID.Contains(idk))
                    {
                        failure = true;
                    }
                }
                if (!failure)
                {
                    List<List<string>> fileList = [["icon_empty", "icon"], ["icon_open"]];
                    List<string> namesList = ["icon", "open"];
                    for (int i = 1; maxLevel >= i; i++)
                    {
                        fileList.Add(["fill-" + i.ToString(), "fill" + i.ToString(), "icon-" + i.ToString()]);
                        namesList.Add("fill" + i.ToString());
                    }
                    bool was = allIdsDrinks[id].changeColor;
                    Drink drink = allIdsDrinks[id];
                    drink.changeColor = true;
                    LoadRSI(drink, sprite, fileList, currentthing, namesList);
                    drink.changeColor = was;
                    if (!drink.states[currentthing].ContainsKey("fill1"))
                    {
                        maxLevel = 0;
                    }
                    drink.fill.Add(currentthing, maxLevel);
                    drink.openState.Add(currentthing, true);
                    drink.maxLevels[currentthing] = maxLevel;
                }
            }
        }
        private async Task LoadDrinks(string drinksPath, bool isMain = false)
        {
            Drink currentDrink = new();
            if (loaded.Contains(drinksPath) || pathblacklist.Contains(drinksPath))
                return;
            loaded.Add(drinksPath);
            bool fileUseless = true;
            string[] content = await File.ReadAllLinesAsync(drinksPath);
            bool skipFirst = false;
            bool skiptillnext = false;
            int index = 0;
            foreach (string oldline in content)
            {
                string line = Modstring(oldline);
                if (line.Contains("- type: reagent") || oldline == content[^1])
                {
                    if (GetName(currentDrink) != "KeyNotFound" && currentDrink.color != "KeyNotFound"  && !blacklist.Contains(currentDrink.id) && skipFirst && GetFlavor(currentDrink) != "KeyNotFound")
                    {
                            if (isMain)
                            {
                                if (await SpriteLoadAndValidate(currentDrink))
                                {
                                    allIdsDrinks.Add(currentDrink.id, currentDrink);
                                    allDrinks.Add(currentDrink);
                                    fileUseless = false;
                                }
                            }
                            else
                            {
                                if (recipeunbuilded.Contains(currentDrink.id) && await SpriteLoadAndValidate(currentDrink))
                                {
                                    allIdsDrinks.Add(currentDrink.id, currentDrink);
                                    recipeunbuilded.Remove(currentDrink.id);
                                    fileUseless = false;
                                }
                            }
                    }
                    skipFirst = true;
                    skiptillnext = false;
                    currentDrink = new();
                }
                else if (!skiptillnext)
                {
                    if (line.Contains("parent: BaseAlcohol"))
                    {
                        currentDrink.flavor = "alcohol";
                        currentDrink.Ethanol = 0.06f;
                        currentDrink.SatiateThirst = 2f;
                    }
                    else if (line.Contains("parent: Milk"))
                    {
                        currentDrink.flavor = "milk";
                        currentDrink.color = "#DFDFDF";
                        currentDrink.physicalDesc = "reagent-physical-desc-opaque";
                        currentDrink.SatiateThirst = 4;
                    }
                    else if (line.Contains("parent: BaseJuice"))
                    {
                        currentDrink.flavor = "sweet";
                    }
                    else if (line.Contains("parent: BaseSoda"))
                    {
                        currentDrink.SatiateThirst = 2;
                    }
                    else if (line.Contains("flavor"))
                    {
                        SetFlavor(currentDrink, Regex.Match(line, pattern).Groups[1].Value, currentDrink.flavor);
                    }
                    else if (line.Contains("physicalDesc"))
                    {
                        currentDrink.physicalDesc = Regex.Match(line, pattern).Groups[1].Value;
                    }
                    else if (line.Contains("name"))
                    {
                        currentDrink.name = Regex.Match(line, pattern).Groups[1].Value;
                    }
                    else if (line.Contains("factor"))
                    {
                        currentDrink.SatiateThirst = float.Parse(Regex.Match(line, pattern).Groups[1].Value, CultureInfo.InvariantCulture);
                    }
                    else if (line.Contains("metamorphicMaxFillLevels"))
                    {
                        currentDrink.maxLevels["glass"] = int.Parse(Regex.Match(line, pattern).Groups[1].Value);
                    }
                    else if (line.Contains("sprite:"))
                    {
                        currentDrink.sprite = Regex.Match(line, pattern).Groups[1].Value.Replace("Objects/Consumable/Drinks/", "") + "/";
                    }
                    else if (line.Contains("id:"))
                    {
                        currentDrink.id = Regex.Match(line, pattern).Groups[1].Value.Trim();
                    }
                    else if (line.Contains("metamorphicChangeColor: "))
                    {
                        currentDrink.changeColor = bool.Parse(Regex.Match(line, pattern).Groups[1].Value);
                    }
                    else if (line.Contains("color: "))
                    {
                        currentDrink.color = Regex.Match(line, pattern).Groups[1].Value.Replace("\"", "").Trim().ToUpper();
                        if (!currentDrink.color.Contains('#'))
                        {
                            currentDrink.color = GetColor(currentDrink.color.ToLower());
                        }
                    }
                    else if (line.Contains("Poison: "))
                    {
                        currentDrink.Poison = float.Parse(Regex.Match(line, pattern).Groups[1].Value, CultureInfo.InvariantCulture);
                    }
                    else if (line.Contains("- !type:AdjustReagent") && content[index + 1].Contains("reagent: Ethanol"))
                    {
                        currentDrink.Ethanol = float.Parse(Regex.Match(Modstring(content[index + 2]), pattern).Groups[1].Value, CultureInfo.InvariantCulture);
                    }
                    else if (line.Contains(" Alcohol:"))
                    {
                        skiptillnext = true;
                    }
                }
                index++;
            }
            if (fileUseless)
                pathblacklist.Add(drinksPath);
        }

        private void LoadPalette(string path)
        {
            int index = 0;
            string[] content = File.ReadAllLines(path);
            bool founded = false;
            bool colorsPassed = false;
            foreach (string line in content)
            {
                if (line.Contains("- type: palette"))
                {
                    colorsPassed = false;
                    founded = false;
                    if (content[index + 1].Contains("id: "))
                    {
                        founded = true;
                    }
                }
                if (founded && colorsPassed)
                {
                    string[] parts = line.Split([':'], 2);
                    if (parts.Length == 2)
                    {
                        parts[0] = parts[0].Replace("\"", "").Replace("#", "").Trim();
                        parts[1] = parts[1].Replace("\"", "").Trim();
                        if (palette.ContainsKey(parts[0]))
                        {
                            palette[parts[0]] = parts[1];
                        }
                        else
                        {
                            palette.Add(parts[0], parts[1]);
                        }
                    }
                }
                if (line.Contains("colors:"))
                {
                    colorsPassed = true;
                }
                index++;
            }
        }
        private void LoadRecipe(string path)
        {
            string[] content = File.ReadAllLines(path);
            bool skipFirst = false;
            int index = 0;
            bool recipeBuild = false;
            string name = "";
            short amount = 0;
            bool reset = false;
            bool skipFirstRecipe = false;
            foreach (string oldline in content)
            {
                string line = Modstring(oldline);
                if (line.Contains("products:"))
                {
                    recipeBuild = false;
                    skipFirstRecipe = false;
                    currentRecipe.output = short.Parse(Regex.Match(Modstring(content[index + 1]), pattern).Groups[1].Value, CultureInfo.InvariantCulture);
                    currentRecipe.input.Add(name, amount);
                }
                if (recipeBuild)
                {
                    if (line.Trim() != "")
                    {
                        if (!reset)
                        {
                            if (skipFirstRecipe)
                            {
                                currentRecipe.input.Add(name, amount);
                            }
                            else
                            {
                                skipFirstRecipe = true;
                            }
                            name = line[..line.IndexOf(':')].Trim();
                            if (!allIdsDrinks.ContainsKey(name))
                            {
                                recipeunbuilded.Add(name);
                            }
                        }
                        else
                        {
                            amount = short.Parse(Regex.Match(line, pattern).Groups[1].Value, CultureInfo.InvariantCulture);
                        }
                        reset = !reset;
                    }
                }
                else
                {
                    if (line.Contains("- type: reaction"))
                    {
                        if (skipFirst && allIdsDrinks.TryGetValue(currentRecipe.id, out Drink? value))
                        {
                            recipe.Add(value, currentRecipe);
                        }
                        skipFirst = true;
                        currentRecipe = new();
                    }
                    if (line.Contains("- Stir"))
                    {
                        currentRecipe.action = "Stir";
                    }
                    if (line.Contains("- Shake"))
                    {
                        currentRecipe.action = "Shake";
                    }
                    if (line.Contains("id: "))
                    {
                        currentRecipe.id = Regex.Match(line, pattern).Groups[1].Value;
                    }
                    if (line.Contains("reactants:"))
                    {
                        recipeBuild = true;
                    }
                }
                index++;
            }
        }
        private async Task LoadRSIAsync(Drink drink, string path,  List<List<string>> name, string currentNamespace, List<string> namenew)
        {
            drink.states.Add(currentNamespace, []);
            drink.statesCache.Add(currentNamespace, []);
            string totalpath = "./Textures/" + path;
            if (Directory.Exists(totalpath))
            {
                metaRSIJSON tmpjson = JsonSerializer.Deserialize<metaRSIJSON>(await File.ReadAllTextAsync(totalpath + "meta.json"))!;
                int i = 0;
                foreach (string totalname in namenew)
                {
                    foreach (string filename in name[i])
                    {
                        bool founded = false;
                        if (File.Exists(totalpath + filename+".png"))
                        {
                            drink.states[currentNamespace].Add(totalname, []);
                            BitmapImage tmp = new (new Uri(totalpath + filename+".png", UriKind.Relative));
                            foreach (var state in tmpjson.states)
                            {
                                if (state.name == filename)
                                {
                                    drink.statesCache[currentNamespace].Add(totalname, state);
                                    state.name = totalname;
                                    if (state.delays is null)
                                    {
                                        CroppedBitmap result;
                                        if (drink.changeColor && totalname.Contains("fill"))
                                            result = ImageProcessingHelper.ColorizeGrayscaleImage(CropImage(tmp, 0), drink.color);
                                        else
                                            result = CropImage(tmp, 0);
                                        drink.states[currentNamespace][totalname].Add(0, result);
                                        founded = true;
                                        break;
                                    }
                                    else
                                    {
                                        short delayIndex = 0;
                                        foreach (var delaySet in state.delays[0])
                                        {
                                            CroppedBitmap result;
                                            if (drink.changeColor && totalname.Contains("fill"))
                                                result = ImageProcessingHelper.ColorizeGrayscaleImage(CropImage(tmp, delayIndex), drink.color);
                                            else
                                                result = CropImage(tmp, delayIndex);
                                            drink.states[currentNamespace][totalname].Add(delayIndex, result);
                                            delayIndex++;
                                        }
                                        founded = true;
                                        break;
                                    }
                                }
                            }
                        }
                        if (founded) break;
                    }
                    i++;
                }
            }
        }
        private static void LoadRSI(Drink drink, string path, List<List<string>> name, string currentNamespace, List<string> namenew)
        {
            drink.states.Add(currentNamespace, []);
            drink.statesCache.Add(currentNamespace, []);
            string totalpath = "./Textures/" + path;
            if (Directory.Exists(totalpath))
            {
                metaRSIJSON tmpjson = JsonSerializer.Deserialize<metaRSIJSON>(File.ReadAllText(totalpath + "meta.json"))!;
                int i = 0;
                foreach (string totalname in namenew)
                {
                    foreach (string filename in name[i])
                    {
                        bool founded = false;
                        if (File.Exists(totalpath + filename + ".png"))
                        {
                            drink.states[currentNamespace].Add(totalname, []);
                            BitmapImage tmp = new(new Uri(totalpath + filename + ".png", UriKind.Relative));
                            foreach (var state in tmpjson.states)
                            {
                                if (state.name == filename)
                                {
                                    drink.statesCache[currentNamespace].Add(totalname, state);
                                    state.name = totalname;
                                    if (state.delays is null)
                                    {
                                        CroppedBitmap result;
                                        if (drink.changeColor && totalname.Contains("fill"))
                                            result = ImageProcessingHelper.ColorizeGrayscaleImage(CropImage(tmp, 0), drink.color);
                                        else
                                            result = CropImage(tmp, 0);
                                        drink.states[currentNamespace][totalname].Add(0, result);
                                        founded = true;
                                        break;
                                    }
                                    else
                                    {
                                        short delayIndex = 0;
                                        foreach (var delaySet in state.delays[0])
                                        {
                                            CroppedBitmap result;
                                            if (drink.changeColor && totalname.Contains("fill"))
                                                result = ImageProcessingHelper.ColorizeGrayscaleImage(CropImage(tmp, delayIndex), drink.color);
                                            else
                                                result = CropImage(tmp, delayIndex);
                                            drink.states[currentNamespace][totalname].Add(delayIndex, result);
                                            delayIndex++;
                                        }
                                        founded = true;
                                        break;
                                    }
                                }
                            }
                        }
                        if (founded) break;
                    }
                    i++;
                }
            }
        }

        private static CroppedBitmap CropImage(BitmapImage image, int index)
        {
            int columns = (int)image.PixelWidth / 32;
            Int32Rect sizerect = new((index % columns) * 32, (index / columns) * 32, 32, 32);
            CroppedBitmap imageresult = new(image, sizerect);
            imageresult.Freeze();
            return imageresult;
        }
        private async Task ReloadPrototypes(string path)
        {
            string[] files = Directory.GetFiles(path);
            await Parallel.ForEachAsync(files, async (path, token) =>
            {
                await LoadDrinks(path);
            }).ConfigureAwait(false);
            string[] directories = Directory.GetDirectories(path);
            foreach (string directory in directories)
            {
                await ReloadPrototypes(directory);
            }
        }
        private void LoadFlavors(string path)
        {
            string[] content = File.ReadAllLines(path);
            bool skipFirst = false;
            string id = "";
            string flavor = "";

            foreach (string oldline in content)
            {
                string line = Modstring(oldline);
                if (line.Contains("- type: flavor"))
                {
                    if (skipFirst)
                    {
                        flavors.Add(id, flavor);
                    }
                    skipFirst = true;
                }
                else if (line.Contains("id: "))
                {
                    id = Regex.Match(line, pattern).Groups[1].Value.Trim();
                }
                else if (line.Contains("description: "))
                {
                    flavor = Regex.Match(line, pattern).Groups[1].Value.Trim();
                }
            }
        }
        private static void Log(string content)
        {
            Debug.WriteLine(content);
        }
        private void Reloadlocale(string path)
        {
            string[] files = Directory.GetFiles(path);
            foreach (string file in files)
            {
                LoadLocalization(file);
            }
            string[] directories = Directory.GetDirectories(path);
            foreach (string directory in directories)
            {
                Reloadlocale(directory);
            }
        }
        private void ReloadPalettes(string path)
        {
            string[] files = Directory.GetFiles(path);
            foreach (string file in files)
            {

                LoadPalette(file);
            }
            string[] directories = Directory.GetDirectories(path);
            foreach (string directory in directories)
            {
                ReloadPalettes(directory);
            }
        }


        private async Task LoadDrinks()
        {
            string[] paths = ["./Prototypes/Corvax/Reagents/Consumable\\Drink\\alcohol.yml",
            "./Prototypes/Reagents/Consumable\\Drink\\alcohol.yml",
            "./Prototypes/Corvax/Reagents/Consumable\\Drink\\drinks.yml",
            "./Prototypes/Reagents/Consumable\\Drink\\drinks.yml",
            "./Prototypes/Reagents/Consumable\\Drink\\juice.yml",
            "./Prototypes/Reagents/Consumable\\Drink\\soda.yml"];
            await Parallel.ForEachAsync(paths, async (path, token) =>
            {
                await LoadDrinks(path, true);
            }).ConfigureAwait(false);
        }

        private async Task LoadDepences()
        {
            string[] paths = ["./Prototypes/Reagents/",
            "./Prototypes/Corvax/Reagents/"];
            await Parallel.ForEachAsync(paths, async (path, token) =>
            {
                await ReloadPrototypes(path);
            }).ConfigureAwait(false);

            File.WriteAllLines("./Cache/BlacklistedFiles.lol", pathblacklist);

            //Hell, that's a lot of assets

            allDrinks.Sort((x, y) => StringComparer.InvariantCulture.Compare(GetName(x), GetName(y)));
            for (int i = 0; i < allDrinks.Count; i++)
            {
                Drink currentDrink = allDrinks[i];
                currentDrink.index = i;
                string first = GetName(currentDrink)[..1];
                if (!bynamedict.TryGetValue(first, out List<Drink>? value))
                {
                    value = [];
                    bynamedict.Add(first, value);
                }
                if (!byflavordict.ContainsKey(CapitalizeWords(GetFlavor(currentDrink))))
                {
                    byflavordict.Add(CapitalizeWords(GetFlavor(currentDrink)), []);
                }
                if (!byethanoldict.ContainsKey(currentDrink.Ethanol.ToString(CultureInfo.InvariantCulture)))
                {
                    byethanoldict.Add(currentDrink.Ethanol.ToString(CultureInfo.InvariantCulture), []);
                }

                value.Add(currentDrink);
                byflavordict[CapitalizeWords(GetFlavor(currentDrink))].Add(currentDrink);
                if (!byethanolblacklist.Contains(currentDrink.id))
                    byethanoldict[currentDrink.Ethanol.ToString(CultureInfo.InvariantCulture)].Add(currentDrink);
            }
            var keysToRemove = new List<string>();
            foreach (var list in byflavordict)
            {
                if (list.Value.Count == 1)
                {
                    keysToRemove.Add(list.Key);
                }
            }
            foreach (var key in keysToRemove)
            {
                byflavordict.Remove(key);
            }
            var keysToRemoveDrink = new List<Drink>();
            foreach (Drink drink in byethanoldict["0"])
            {
                if (!recipe.ContainsKey(drink))
                {
                    keysToRemoveDrink.Add(drink);
                }
            }
            foreach (var key in keysToRemoveDrink)
            {
                byethanoldict["0"].Remove(key);
            }
        }

#pragma warning disable CS8618
        public MainWindow()
#pragma warning restore CS8618
        {
            for (int i = 1; i<=9; i++)
            {
                List<string> tmp = ["icon"];
                for (int j = 1; j <= i; j++)
                {
                    tmp.Add("fill" + j.ToString());
                }
                namesLists.Add(i, tmp);
            }
            for (int i = 1; i<=9; i++)
            {
                List<string> tmp = ["icon","open"];
                for (int j = 1; j <= i; j++)
                {
                    tmp.Add("fill" + j.ToString());
                }
                namesListsAdditional.Add(i, tmp);
            }
            for (int i = 1; i<=9; i++)
            {
                List<List<string>> tmp = [["icon_empty","icon"]];
                for (int j = 1; j <= i; j++)
                {
                    tmp.Add(["fill-" + j.ToString(), "fill" + j.ToString()]);
                }
                fileLists.Add(i, tmp);
            }
            for (int i = 1; i<=9; i++)
            {
                List<List<string>> tmp = [["icon_empty","icon"], ["icon_open"]];
                for (int j = 1; j <= i; j++)
                {
                    tmp.Add(["fill-" + j.ToString(), "fill" + j.ToString(), "icon-" + j.ToString()]);
                }
                fileListsAdditional.Add(i, tmp);
            }
            if (!Directory.Exists("./Cache/"))
                Directory.CreateDirectory("./Cache/");
            if (File.Exists("./Cache/BlacklistedFiles.lol"))
            {
                string[] content = File.ReadAllLines("./Cache/BlacklistedFiles.lol");
                foreach (string line in content)
                    pathblacklist.Add(line);
            }

            LoadFlavors("./Prototypes/flavors.yml");
            ReloadPalettes("./Prototypes/Palettes/");
            Reloadlocale("./Locale/");
            LoadDrinks().GetAwaiter().GetResult();

            LoadRecipe("./Prototypes/drinks.yml");
            LoadRecipe("./Prototypes/Corvax/drinks.yml");

            LoadDepences();
            LoadAdditionalContainers("./Prototypes/drinks_bottles.yml");
            LoadAdditionalContainers("./Prototypes/Corvax/drinks_bottles.yml");
            LoadAdditionalContainers("./Prototypes/drinks_cans.yml");
            LoadAdditionalContainers("./Prototypes/drinks-cartons.yml");
            arrowleftimage.BeginInit();
            arrowleftimage.UriSource = new Uri("/Images/shop_arrow_spr.png", UriKind.RelativeOrAbsolute);
            arrowleftimage.Rotation = Rotation.Rotate180;
            arrowleftimage.EndInit();
            arrowrightimage.BeginInit();
            arrowrightimage.UriSource = new Uri("/Images/shop_arrow_spr.png", UriKind.RelativeOrAbsolute);
            arrowrightimage.EndInit();
            arrowhistoryimage.BeginInit();
            arrowhistoryimage.UriSource = new Uri("/Images/spr_textbox_arrow.png", UriKind.RelativeOrAbsolute);
            arrowhistoryimage.Rotation = Rotation.Rotate180;
            arrowhistoryimage.EndInit();
            jokerge.BeginInit();
            jokerge.UriSource = new Uri("/Images/jokerge.png", UriKind.RelativeOrAbsolute);
            jokerge.EndInit();
            circle.BeginInit();
            circle.UriSource = new Uri("/Images/circle.png", UriKind.RelativeOrAbsolute);
            circle.EndInit();
            morty.BeginInit();
            morty.UriSource = new Uri("/Images/morty.png", UriKind.RelativeOrAbsolute);
            morty.EndInit();

            InitializeComponent();
            iconFront = new(new Uri("/Images/icon-front.png", UriKind.RelativeOrAbsolute));
            byethanol.FontFamily = font;
            byname.FontFamily = font;
            byflavor.FontFamily = font;
            exit.FontFamily = font;
            random.FontFamily = font;
            filterbuttonstyle = (Style)resourceDictionary["FilterButtonStyle"];
            MainGrid.Children.Add(secondGrid);
            RenderOptions.SetBitmapScalingMode(IntroImage, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetBitmapScalingMode(BGImage, BitmapScalingMode.NearestNeighbor);
            AudioManager.PlaySound(welcomeSound);
        }
        private void RandomButtonClick(object sender, RoutedEventArgs e)
        {
            StandartClick(MainGrid.Children.IndexOf((Button)sender));
            amount = 30f;
            ResetSpriteTimer();
            DrawDrink(allDrinks[rand.Next(allDrinks.Count)]);
        }
        private void StandartClick(int newSelected)
        {
            AudioManager.PlaySound("jukeselect.wav");
            IntroImage.Opacity = 0;
            selected = newSelected;
            if (selectedWas != selected)
            {
                ((Button)MainGrid.Children[selected]).Background = Brushes.Black;
                ((Button)MainGrid.Children[selected]).Foreground = Brushes.White;
                if (selectedWas > 0)
                {
                    ((Button)MainGrid.Children[selectedWas]).Background = Brushes.White;
                    ((Button)MainGrid.Children[selectedWas]).Foreground = Brushes.Black;
                }
                selectedWas = selected;
            }
        }
        private static string CapitalizeWords(string input)
        {
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(input.ToLower());
        }
        private void BuildGrid(SortedDictionary<string, List<Drink>> gridcontent, Grid gridlol)
        {
            gridlol.Children.Clear();
            while (gridlol.RowDefinitions.Count > 0)
            {
                gridlol.RowDefinitions.RemoveAt(0);
            }

            while (gridlol.ColumnDefinitions.Count > 0)
            {
                gridlol.ColumnDefinitions.RemoveAt(0);
            }
            int columns = (int)Math.Ceiling(gridcontent.Count / (double)6);
            int maxRows = 7;
            for (int i = 0; i < maxRows; i++)
            {
                gridlol.RowDefinitions.Add(new RowDefinition() { MaxHeight = 45 });
            }
            int currentColumn = 0;
            int currentRow = 0;
            gridlol.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto, MinWidth = 120 });
            foreach (string drink in gridcontent.Keys)
            {
                Button tmp = new()
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = Brushes.Gray,
                    FontFamily = font,
                    Style = filterbuttonstyle,
                    FontSize = 18,
                    Height = 35,
                    Tag = drink,
                    Content = drink
                };
                tmp.Click += ClickFilterButton;
                tmp.MouseEnter += AnyMouseEnter;
                tmp.MouseLeave += AnyMouseLeave;
                gridlol.Children.Add(tmp);
                Grid.SetRow(tmp, currentRow);
                Grid.SetColumn(tmp, currentColumn);
                currentRow++;
                if (currentRow >= maxRows)
                {
                    currentRow = 0;
                    currentColumn++;
                    gridlol.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto, MinWidth = 120 });
                }
            }
        }

        private void AnyTopBarClick(object sender, RoutedEventArgs e)
        {
            ResetSpriteTimer();
            drinksHistory = [];
            StandartClick(MainGrid.Children.IndexOf((Button)sender));
            if (filtersection != 1)
            {
                secondGrid.Children.Clear();
                TextBlock searchbyLabel = new()
                {
                    FontFamily = font,
                    Margin = new Thickness(28, 56, 24, 40),
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    FontSize = 25
                };
                filtersection = 1;
                Grid filterNames = new() { Margin = new Thickness(24, 90, 24, 20) };
                secondGrid.Children.Add(searchbyLabel);
                secondGrid.Children.Add(filterNames);
            }
            currentDrinks = ((Button)sender).Name;
            switch (((Button)sender).Name)
            {
                case "byname":
                    ((TextBlock)secondGrid.Children[0]).Text = "Поиск по: имени";
                    BuildGrid(bynamedict, (Grid)secondGrid.Children[1]);
                    break;
                case "byethanol":
                    ((TextBlock)secondGrid.Children[0]).Text = "Поиск по: крепости";
                    BuildGrid(byethanoldict, (Grid)secondGrid.Children[1]);
                    break;
                case "byflavor":
                    ((TextBlock)secondGrid.Children[0]).Text = "Поиск по: вкусу";
                    BuildGrid(byflavordict, (Grid)secondGrid.Children[1]);
                    break;
            }

        }

        private void Exitclick(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void AnyTopBarMouseEnter(object sender, MouseEventArgs e)
        {
            ((Button)sender).Background = Brushes.Black;
            ((Button)sender).Foreground = Brushes.White;
            AudioManager.PlaySound(hoverSound);
        }
        private void AnyTopBarMouseLeave(object sender, MouseEventArgs e)
        {
            if (MainGrid.Children.IndexOf((Button)sender) != selected)
            {
                ((Button)sender).Foreground = Brushes.Black;
                ((Button)sender).Background = Brushes.White;
            }
        }
        private void AnyMouseEnter(object sender, MouseEventArgs e)
        {
            ((Button)sender).Foreground = Brushes.White;
            AudioManager.PlaySound(hoverSound);
        }

        private void AnyMouseLeave(object sender, MouseEventArgs e)
        {
            ((Button)sender).Foreground = Brushes.Gray;
        }
        private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !MyRegex().IsMatch(((TextBox)sender).Text + e.Text); ;
        }
        private void ClickFilterButton(object sender, RoutedEventArgs e)
        {
            AudioManager.PlaySound(clickFilterButtonSound);
            filtersection = 2;
            secondGrid.Children.Clear();
            Grid gridlol = new()
            {
                Margin = new Thickness(24, 90, 24, 40)
            };
            Grid topbargrid = new()
            {
                Margin = new Thickness(28, 44, 35, 378)
            };
            topbargrid.RowDefinitions.Add(new RowDefinition());
            topbargrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            topbargrid.ColumnDefinitions.Add(new ColumnDefinition());
            topbargrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            TextBlock name = new()
            {
                FontFamily = font,
                FontSize = 25,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.White
            };
            switch (currentDrinks)
            {
                case "byethanol":
                    name.Text = "По крепкости: " + (String)((Button)sender).Tag;
                    break;
                case "byname":
                    name.Text = "По имени: " + (String)((Button)sender).Tag;
                    break;
                case "byflavor":
                    name.Text = "По вкусу: " + (String)((Button)sender).Tag;
                    break;
            }
            TextBlock Drinkname = new()
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = font,
                FontSize = 25,
                Foreground = Brushes.White
            };
            Grid.SetColumn(Drinkname, 1);
            Button topbarbutton = new()
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.White,
                FontFamily = font,
                Style = filterbuttonstyle,
                FontSize = 25,
                Height = 35,
                Content = "Случайный"
            };
            topbarbutton.MouseEnter += AnyMouseEnter;
            Grid.SetColumn(Drinkname, 1);
            Grid.SetColumn(topbarbutton, 2);
            topbargrid.Children.Add(Drinkname);
            topbargrid.Children.Add(topbarbutton);
            topbargrid.Children.Add(name);
            secondGrid.Children.Add(topbargrid);
            secondGrid.Children.Add(gridlol);
            while (gridlol.RowDefinitions.Count > 0)
            {
                gridlol.RowDefinitions.RemoveAt(0);
            }
            List<Drink> currentDrinksList = GetCurrentDrinks((String)((Button)sender).Tag);
            topbarbutton.Click += (s, e) =>
            {
                AudioManager.PlaySound(clickSubFilterButtonSound);
                amount = 30f;
                DrawDrink(currentDrinksList[rand.Next(currentDrinksList.Count)]);
            };

            while (gridlol.ColumnDefinitions.Count > 0)
            {
                gridlol.ColumnDefinitions.RemoveAt(0);
            }
            int columns = (int)Math.Ceiling(currentDrinksList.Count / (double)6);
            int maxRows = 8;
            for (int i = 0; i < maxRows; i++)
            {
                gridlol.RowDefinitions.Add(new RowDefinition() { MaxHeight = 45 });
            }
            int currentColumn = 0;
            int currentRow = 0;
            gridlol.ColumnDefinitions.Add(new ColumnDefinition());
            foreach (Drink drink in currentDrinksList)
            {
                Button tmp = new()
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = Brushes.Gray,
                    FontFamily = font,
                    Style = filterbuttonstyle,
                    FontSize = 18,
                    Height = 35,
                    Content = GetName(drink),
                    Tag = drink
                };
                tmp.MouseEnter += AnyMouseEnter;
                tmp.MouseEnter += Subfilterbutton;
                tmp.MouseLeave += AnyMouseLeave;
                tmp.Click += Subfilterclick;
                Grid.SetRow(tmp, currentRow);
                Grid.SetColumn(tmp, currentColumn);
                currentRow++;
                if (currentRow >= maxRows)
                {
                    currentRow = 0;
                    currentColumn++;
                    gridlol.ColumnDefinitions.Add(new ColumnDefinition());
                }
                gridlol.Children.Add(tmp);
            }
            if (currentColumn * maxRows == currentDrinksList.Count && currentRow == 0)
            {
                gridlol.ColumnDefinitions.RemoveAt(gridlol.ColumnDefinitions.Count - 1);
            }
        }
        private void Subfilterbutton(object sender, RoutedEventArgs e)
        {
            if (currentDrinks != "byflavor")
                ((TextBlock)((Grid)((Grid)((Grid)((Button)sender).Parent).Parent).Children[0]).Children[0]).Text = GetName((Drink)((Button)sender).Tag);
        }
        static string WrapText(string text, int maxLineLength)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            string result = "";
            int currentIndex = 0;

            while (currentIndex < text.Length)
            {
                int endIndex = currentIndex + maxLineLength;

                if (endIndex >= text.Length)
                {
                    result += text[currentIndex..];
                    break;
                }

                int lastSpaceIndex = text.LastIndexOf(' ', endIndex, endIndex - currentIndex);

                if (lastSpaceIndex > currentIndex)
                {
                    result += string.Concat(text.AsSpan(currentIndex, lastSpaceIndex - currentIndex), Environment.NewLine);
                    currentIndex = lastSpaceIndex + 1;
                }
                else
                {
                    result += string.Concat(text.AsSpan(currentIndex, maxLineLength), Environment.NewLine);
                    currentIndex += maxLineLength;
                }
            }

            return result.TrimEnd();
        }
        private bool skipFirst = false;
        private TextBlock middleArrowTextSprite;
        private Button openbutton;
        private Button botletoggle;
        private System.Windows.Controls.Image drinkFillSprite;
        private System.Windows.Controls.Image drinkGlassSprite;
        private System.Windows.Controls.Image onsprite;
        private short spritenum = 0;
        private string fillstring;
        private bool inArrow = false;
        private int currentfillness;
        private void DrawDrink(Drink drink)
        {
            spritenum = 0;
            drink.hyperlinkson = false;
            currentDrinkSelected = drink;
            int currentfillness = drink.fill[drink.state];
            string fillstring = GetCurrentFillnesString();
            filtersection = 3;
            secondGrid.Children.Clear();
            Grid namegrid = new()
            {
                Margin = new Thickness(24, 41, 20, 20)
            };
            namegrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
            namegrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
            namegrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            TextBlock name = new()
            {
                Margin = new Thickness(24, 46, 20, 20),
                FontFamily = font,
                FontSize = 33,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Foreground = Brushes.White,
                Text = GetName(drink)
            };
            secondGrid.Children.Add(name);
            Grid lowertextgrid = new()
            {
                Margin = new Thickness(23, 20, 20, 70),
                VerticalAlignment = VerticalAlignment.Bottom
            };
            TextBlock description = new()
            {
                FontFamily = font,
                FontSize = 23,
                HorizontalAlignment = HorizontalAlignment.Left,
                Foreground = Brushes.White,
                Text = WrapText(GetDesc(drink), 52)
            };
            TextBlock lowerText = new()
            {
                FontFamily = font,
                FontSize = 23,
                HorizontalAlignment = HorizontalAlignment.Left,
                Foreground = Brushes.White,
            };
            if (drink.Poison > 0)
                lowerText.Text = WrapText("На вкус " + GetFlavor(drink, true) + ". На вид " + GetPthysDesc(drink) + ". " + drink.Ethanol.ToString(CultureInfo.InvariantCulture) + " этанола. Утоляет жажду на " + drink.SatiateThirst.ToString() + ". Отравляет на " + drink.Poison.ToString(CultureInfo.InvariantCulture) + ".", 52);
            else
                lowerText.Text = WrapText("На вкус " + GetFlavor(drink, true) + ". На вид " + GetPthysDesc(drink) + ". " + drink.Ethanol.ToString(CultureInfo.InvariantCulture) + " этанола. Утоляет жажду на " + drink.SatiateThirst.ToString() + ".", 52);
            lowertextgrid.RowDefinitions.Add(new RowDefinition() { MaxHeight = 94, Height = GridLength.Auto });
            lowertextgrid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(9) });
            lowertextgrid.RowDefinitions.Add(new RowDefinition());
            Grid.SetRow(lowerText, 2);
            lowertextgrid.Children.Add(lowerText);
            lowertextgrid.Children.Add(description);
            Grid arrowsthinggrid = new() 
            {
                Margin = new Thickness(22, 0, 0, 28),
                VerticalAlignment = VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            arrowsthinggrid.ColumnDefinitions.Add(new ColumnDefinition() { Width =new(30)});
            arrowsthinggrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new(7) });
            arrowsthinggrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            arrowsthinggrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new(7) });
            arrowsthinggrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new(30) });
            arrowsthinggrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new(7) });
            arrowsthinggrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            arrowsthinggrid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto});
            arrowsthinggrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
            Button arrowLeft = new()
            {
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0,
                Style = filterbuttonstyle,
                Width = 30,
                Height = 30
            };
            System.Windows.Controls.Image arrowLeftImage = new()
            {
                VerticalAlignment = VerticalAlignment.Center,
                Source = arrowleftimage,
                Width = 30,
                Height = 30
            };
            Button arrowRight = new()
            {
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0,
                Style = filterbuttonstyle,
                Width = 30,
                Height = 30
            };
            System.Windows.Controls.Image arrowRightImage = new()
            {
                VerticalAlignment = VerticalAlignment.Center,
                Source = arrowrightimage,
                Height = 30
            };
            Grid.SetColumn(arrowRight, 4);
            Grid.SetColumn(arrowRightImage,4);
            TextBlock middleArrowText = new()
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = font,
                FontSize = 21,
                Foreground = Brushes.White,
                Text = (drink.index + 1).ToString() + "/" + allDrinks.Count.ToString()
            };
            Grid.SetColumn(middleArrowText, 2);
            arrowsthinggrid.Children.Add(arrowRightImage);
            arrowsthinggrid.Children.Add(arrowRight);
            arrowsthinggrid.Children.Add(middleArrowText);
            arrowsthinggrid.Children.Add(arrowLeftImage);
            arrowsthinggrid.Children.Add(arrowLeft);
            arrowLeft.Click += (s, e) =>  DrawOffset(drink, -1);;
            arrowRight.Click += (s, e) => DrawOffset(drink, 1);;
            arrowLeft.MouseEnter += (s,e) => {
                inArrow = true;
                if (!skipFirst)
                {
                    AudioManager.PlaySound(hoverSound);
                }
                else
                    skipFirst = false;
            };
            arrowRight.MouseEnter += (s, e) => {
                inArrow = true;
                if (!skipFirst)
                {
                    AudioManager.PlaySound(hoverSound);
                }
                else
                    skipFirst = false;
            };
            arrowRight.MouseLeave += (s, e) => inArrow = false;
            arrowLeft.MouseLeave += (s, e) => inArrow = false;
            TextBox amountChange = new()
            {
                FontSize = 23,
                Width = 50,
                Height = 17,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Background = Brushes.Black,
                Foreground = Brushes.White,
                MaxLength = 4,
                Text = amount.ToString(CultureInfo.InvariantCulture),
                FontFamily = font,
                BorderThickness = new Thickness(0)
            };
            amountChange.PreviewTextInput += (s, e) =>
            {
                if (!(int.TryParse(((TextBox)s).Text + e.Text, out int value) && value >= 0 && value <= 100 || "1984".Contains(((TextBox)s).Text + e.Text)))
                { e.Handled = true; }
            };
            amountChange.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    if (short.TryParse(((TextBox)s).Text, out short parsedNum))
                    {
                        if (((TextBox)s).Text == "1984") // СЛАВА МОРТИ БОЖЕ ХРАНИ ЗАМХОСТА
                        {
                            System.Windows.Controls.Image tmp = new()
                            {
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Center,
                                Source = circle,
                                Width = 300
                            };
                            System.Windows.Controls.Image tmp2 = new()
                            {
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Center,
                                Source = morty,
                                Width = 400
                            };
                            DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(50) };
                            MainGrid.Children.Add(tmp2);
                            MainGrid.Children.Add(tmp);
                            bool toggle = false;
                            timer.Tick += (s, e) => { if (toggle) { tmp.Opacity = 0; } else { tmp.Opacity = 1; } toggle = !toggle; };
                            timer.Start();
                            AudioManager.PlaySound("vineboom.wav", () =>
                            {
                                timer.Stop();
                                MainGrid.Children.Remove(tmp2);
                                MainGrid.Children.Remove(tmp);
                            });
                            ((TextBox)s).Text = amount.ToString();
                        }
                        else
                        {
                            amount = parsedNum;
                            AudioManager.PlaySound(yeahSound);
                            DrawDrink(drink);
                        }
                    }
                    else
                    {
                        ((TextBox)s).Text = amount.ToString();
                        AudioManager.PlaySound("click_sound.wav");
                    }
                }
            };
            TextBlock amountChangeText = new()
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                FontFamily = font,
                FontSize = 23,
                Foreground = Brushes.White,
                Text = "Количество:"
            };
            Grid imagesprtiegrid = new()
            {
                Margin = new Thickness(0, 50, 30, 0),
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            imagesprtiegrid.ColumnDefinitions.Add(new ColumnDefinition());
            imagesprtiegrid.ColumnDefinitions.Add(new ColumnDefinition());
            imagesprtiegrid.ColumnDefinitions.Add(new ColumnDefinition());
            imagesprtiegrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
            imagesprtiegrid.RowDefinitions.Add(new RowDefinition() { Height = new(8) });
            imagesprtiegrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
            imagesprtiegrid.RowDefinitions.Add(new RowDefinition() { Height = GridLength.Auto });
            imagesprtiegrid.RowDefinitions.Add(new RowDefinition());
            drinkGlassSprite = new()
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Width = 160,
                Source = drink.GetSprite("icon")
            };
            drinkFillSprite = new()
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Width = 160,
            };
            if (drink.states[drink.state].ContainsKey("fill1") && currentfillness > 0)
            {
                drinkFillSprite.Source = drink.GetSprite(fillstring, spritenum);
            }
            Grid.SetColumnSpan(drinkGlassSprite, 3);
            Grid.SetColumnSpan(drinkFillSprite, 3);
            Button arrowLeftSprite = new()
            {
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0,
                Style = filterbuttonstyle,
                Width = 30,
                Height = 30
            };
            System.Windows.Controls.Image arrowLeftImageSprite = new()
            {
                VerticalAlignment = VerticalAlignment.Center,
                Source = arrowleftimage,
                Width = 30,
                Height = 30
            };
            System.Windows.Controls.Image arrowRightImageSprite = new()
            {
                VerticalAlignment = VerticalAlignment.Center,
                Source = arrowrightimage,
                Stretch = Stretch.Fill,
                Width = 29,
                Height = 30
            };
            Button arrowRightSprite = new()
            {
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0,
                Style = filterbuttonstyle,
                Width = 29,
                Height = 30
            };
            middleArrowTextSprite = new()
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = font,
                FontSize = 21,
                Foreground = Brushes.White,
                Text = currentfillness.ToString() + "/" + drink.maxLevels[drink.state].ToString()
            };
            arrowRightSprite.Click += (s, e) =>
            {
                ChangeFillnes(1);
            };
            arrowLeftSprite.Click += (s, e) =>
            {
                ChangeFillnes(-1);
            };
            Grid.SetRow(arrowLeftSprite, 2);
            Grid.SetRow(arrowLeftImageSprite, 2);

            Grid.SetRow(arrowRightSprite, 2);
            Grid.SetRow(arrowRightImageSprite, 2);
            Grid.SetRow(middleArrowTextSprite, 2);

            Grid.SetColumn(middleArrowTextSprite, 1);
            Grid.SetColumn(arrowRightImageSprite, 2);
            Grid.SetColumn(arrowRightSprite, 2);
            imagesprtiegrid.Children.Add(arrowLeftImageSprite);
            imagesprtiegrid.Children.Add(arrowLeftSprite);
            imagesprtiegrid.Children.Add(arrowRightImageSprite);
            imagesprtiegrid.Children.Add(arrowRightSprite);
            imagesprtiegrid.Children.Add(middleArrowTextSprite);
            imagesprtiegrid.Children.Add(drinkGlassSprite);
            imagesprtiegrid.Children.Add(drinkFillSprite);
            onsprite = new()
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Width = 160,
                Opacity = 0,
                Source = iconFront
            };
            Grid.SetColumnSpan(onsprite, 3);
            RenderOptions.SetBitmapScalingMode(onsprite, BitmapScalingMode.NearestNeighbor);
            imagesprtiegrid.Children.Add(onsprite);
            if (drink.sprite == "glass_clear.rsi/")
                onsprite.Opacity = 1;
            if (drink.states.Count > 1)
            {
                botletoggle = new()
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontFamily = font,
                    Foreground = Brushes.White,
                    Width = 88,
                    Style = filterbuttonstyle
                };
                openbutton = new()
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontFamily = font,
                    Foreground = Brushes.White,
                    Width = 88,
                    Style = filterbuttonstyle,
                    Content = "Открыть"
                };
                if (currentDrinkSelected.openState[currentDrinkSelected.state] == false && drink.states[drink.state].ContainsKey("open"))
                {
                    openbutton.Content = "Закрыть";
                    drinkGlassSprite.Source = drink.GetSprite("open");
                }
                Grid.SetRow(openbutton, 5);
                Grid.SetColumnSpan(openbutton, 3);
                if (drink.states[drink.state].ContainsKey("open"))
                    openbutton.Opacity = 1;
                else
                    openbutton.Opacity = 0;
                switch (drink.state)
                {
                    case "glass":
                        botletoggle.Content = "Стакан";
                        break;
                    case "bottles":
                        botletoggle.Content = "Бутыль";
                        break;
                    case "cans":
                        botletoggle.Content = "Банка";
                        break;
                    case "cartons":
                        botletoggle.Content = "Коробка";
                        break;
                }
                openbutton.MouseEnter += AnyMouseEnter;
                botletoggle.MouseEnter += AnyMouseEnter;
                openbutton.Click += (s, e) =>
                {
                    ToggleContaier();
                };
                botletoggle.Click += (s, e) =>
                {
                    BottleToggle();
                };
                Grid.SetRow(botletoggle, 3);
                Grid.SetColumnSpan(botletoggle, 3);
                imagesprtiegrid.Children.Add(botletoggle);
                imagesprtiegrid.Children.Add(openbutton);
            }
            Grid recipeGrid = new()
            {
                Margin = new Thickness(24, 80, 150, 162),
                Height = 250
            };
            TextBlock RecipeText = new()
            {
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left,
                FontFamily = font,
                FontSize = 25,
                LineHeight = 25,
                Foreground = Brushes.White,
            };
            recipeGrid.Children.Add(RecipeText);
            double Y = 0;
            recipeGrid.MouseWheel += (s, e) =>
            {
                double min = recipeGrid.RenderSize.Height -  RecipeText.RenderSize.Height;
                if (min > 0) return;
                double newof = Math.Min(Y + e.Delta * 0.14,0);
                if (min - newof > 0) newof = min;
                Y = newof;
                RecipeText.Margin = new Thickness(0, Y, 0, 0);
            };
            if (recipe.TryGetValue(drink, out Recipe? drinkRecipe))
            {
                RecipeText.Inlines.Add(new Run("Получение:\n"));
                short totalVolume = 0;
                double coficent = Math.Ceiling(amount / drinkRecipe.output);
                int index = 0;
                foreach (var reagent in drinkRecipe.input)
                {
                    if (allIdsDrinks.TryGetValue(reagent.Key, out Drink? reagentobj))
                    {
                        index++;
                        short toadd = (short)Math.Ceiling(coficent * reagent.Value);
                        totalVolume += toadd;
                        BuildHyperlink(RecipeText, drink, amount, coficent * reagent.Value, reagentobj, GetName(reagentobj) + " (" + toadd.ToString(CultureInfo.InvariantCulture) + ")");
                    }
                    else
                    {
                        Debug.WriteLine(reagent.Key + " MISSING");
                        RecipeText.Inlines.Add("Ой, не нашли ☺\n");
                    }
                }
                SolutionDone(RecipeText, drink, amount, index * 3);
            }
            else if (!allDrinks.Contains(drink))
            {
                RecipeText.Inlines.Add(new Run(WrapText(GetName(drink) + " не является напитком.", 38)));
            }
            else
            {
                RecipeText.Inlines.Add(new Run(WrapText(GetName(drink) + " не получается смешаванием.", 38)));
            }
            if (drinksHistory.Keys.Count > 0)
            {
                TextBlock count = new()
                {
                    FontFamily = font,
                    FontSize = 22,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = Brushes.White,
                    Text = drinksHistory.Count.ToString()
                };
                System.Windows.Controls.Image arrowHistoryImage = new()
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Source = arrowhistoryimage,
                    Height = 20
                };
                name.Margin = new Thickness(52, 46, 20, 20);
                Grid.SetRow(count, 1);
                namegrid.Children.Add(count);
                namegrid.Children.Add(arrowHistoryImage);
                Button arrowHistory = new()
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Opacity = 0,
                    Style = filterbuttonstyle,
                    Height = 20,
                    Width = 20
                };
                arrowHistory.MouseEnter += AnyMouseEnter;
                arrowHistory.Click += (s, e) =>
                {
                    int index = 1;
                    foreach (var idk in drinksHistory)
                    {
                        if (drinksHistory.Keys.Count == index)
                        {
                            Drink tmp = idk.Key;
                            amount = idk.Value;
                            drinksHistory.Remove(idk.Key); 
                            ResetSpriteTimer();
                            AudioManager.PlaySound(clickSubFilterButtonSound);
                            DrawDrink(tmp);
                            break;
                        }
                        index++;
                    }
                };
                namegrid.Children.Add(arrowHistory);
            }
            arrowLeftSprite.MouseEnter += AnyMouseEnter;
            arrowRightSprite.MouseEnter += AnyMouseEnter;
            RenderOptions.SetBitmapScalingMode(drinkGlassSprite, BitmapScalingMode.NearestNeighbor);
            RenderOptions.SetBitmapScalingMode(drinkFillSprite, BitmapScalingMode.NearestNeighbor);
            Grid.SetColumn(amountChange, 7);
            Grid.SetColumn(amountChangeText, 6);
            arrowsthinggrid.Children.Add(amountChange);
            arrowsthinggrid.Children.Add(amountChangeText);
            secondGrid.Children.Add(namegrid);
            secondGrid.Children.Add(lowertextgrid);
            secondGrid.Children.Add(imagesprtiegrid);
            secondGrid.Children.Add(arrowsthinggrid);
            secondGrid.Children.Add(recipeGrid);
            if (drink.states[drink.state].TryGetValue(fillstring, out Dictionary<short, CroppedBitmap>? fill) && fill.Count > 1)
            {
                drinksSpriteTime.Tick += (s, e) =>
                {
                    if (drink.fill[drink.state] > 0)
                    {
                        fillstring = GetCurrentFillnesString();
                        if (spritenum >= fill.Count - 1)
                            spritenum = 0;
                        drinkFillSprite.Source = drink.GetSprite(fillstring, spritenum);
                        drinksSpriteTime.Interval = TimeSpan.FromMilliseconds(drink.GetJSON(fillstring).delays[0][spritenum] * 1000);
                        spritenum++;
                    }
                };
                drinksSpriteTime.Interval = TimeSpan.FromMilliseconds(42);
            }
            if (drink.states[drink.state].TryGetValue("icon", out Dictionary<short, CroppedBitmap>? icon) && icon.Count > 1)
            {
                short spritenumglass = 0;
                drinksSpriteTime.Tick += (s, e) =>
                {
                    if (spritenumglass >= icon.Count - 1)
                        spritenumglass = 0;
                    drinkGlassSprite.Source = drink.GetSprite("icon", spritenumglass); ;
                    drinksSpriteTime.Interval = TimeSpan.FromMilliseconds(drink.GetJSON("icon").delays[0][spritenumglass] * 1000);
                    spritenumglass++;
                };
                drinksSpriteTime.Interval = TimeSpan.FromMilliseconds(42);
            }
            drinksSpriteTime.Start();
        }
        private void ResetSpriteTimer()
        {
            drinksSpriteTime.Stop();
            drinksSpriteTime = new()
            {
                Interval = TimeSpan.FromHours(42)
            };
        }

        private void BottleToggle() 
        {
            bool founded = false;
            bool next = false;
            foreach (var idk in currentDrinkSelected.states)
            {
                if (next)
                {
                    currentDrinkSelected.state = idk.Key;
                    founded = true;
                    break;
                }
                if (idk.Key == currentDrinkSelected.state)
                    next = true;
            }
            if (!founded) currentDrinkSelected.state = "glass";
            fillstring = GetCurrentFillnesString();
            currentfillness = currentDrinkSelected.fill[currentDrinkSelected.state];
            if (currentDrinkSelected.states[currentDrinkSelected.state].ContainsKey("fill1"))
            {
                spritenum = 0;
                drinkFillSprite.Opacity = 1;
                middleArrowTextSprite.Text = currentfillness.ToString() + "/" + currentDrinkSelected.maxLevels[currentDrinkSelected.state].ToString();
                drinkFillSprite.Source = currentDrinkSelected.GetSprite(fillstring, spritenum);
            }
            else
            {
                middleArrowTextSprite.Text = "0/0";
                drinkFillSprite.Opacity = 0;
            }
            openbutton.Content = "Открыть";
            drinkGlassSprite.Source = currentDrinkSelected.GetSprite("icon");
            if (currentDrinkSelected.states[currentDrinkSelected.state].ContainsKey("open"))
            {
                openbutton.Opacity = 1;
                if (!currentDrinkSelected.openState[currentDrinkSelected.state])
                {
                    openbutton.Content = "Закрыть";
                    drinkGlassSprite.Source = currentDrinkSelected.GetSprite("open");
                }
            }
            else
                openbutton.Opacity = 0;
            drinksSpriteTime.Stop();
            switch (currentDrinkSelected.state)
            {
                case "glass":
                    {
                        botletoggle.Content = "Стакан";
                        drinksSpriteTime.Start();
                        if (currentDrinkSelected.sprite == "glass_clear.rsi/")
                            onsprite.Opacity = 1;
                        break;
                    }
                case "bottles":
                    {
                        botletoggle.Content = "Бутыль";
                        onsprite.Opacity = 0;
                        break;
                    }
                case "cans":
                    {
                        botletoggle.Content = "Банка";
                        onsprite.Opacity = 0;
                        break;
                    }
                case "cartons":
                    {
                        botletoggle.Content = "Коробка";
                        onsprite.Opacity = 0;
                        break;
                    }
            }
            AudioManager.PlaySound(clickFilterButtonSound);
        }
        private string GetCurrentFillnesString()
        {
            return "fill" + currentDrinkSelected.fill[currentDrinkSelected.state].ToString();
        }

        private void ChangeFillnes(int amount)
        {
            int totalAmount = currentDrinkSelected.fill[currentDrinkSelected.state] + amount;
            if (totalAmount <= currentDrinkSelected.maxLevels[currentDrinkSelected.state] && totalAmount >= 0)
            {
                AudioManager.PlaySound(clickFilterButtonSound);
                drinkFillSprite.Opacity = 1;
                currentDrinkSelected.fill[currentDrinkSelected.state] += amount;
                currentfillness = currentDrinkSelected.fill[currentDrinkSelected.state];
                fillstring = GetCurrentFillnesString();
                middleArrowTextSprite.Text = currentfillness.ToString() + "/" + currentDrinkSelected.maxLevels[currentDrinkSelected.state].ToString();
                if (currentfillness == 0)
                {
                    drinkFillSprite.Opacity = 0;
                }
                else
                {
                    drinkFillSprite.Source = currentDrinkSelected.GetSprite(fillstring, spritenum);
                }
            }
            else
            {
                AudioManager.PlaySound(errSound);
            }
        }
        private void ToggleContaier()
        {
            if (currentDrinkSelected.states[currentDrinkSelected.state].ContainsKey("open"))
            {
                currentDrinkSelected.openState[currentDrinkSelected.state] = !currentDrinkSelected.openState[currentDrinkSelected.state];
                if (!currentDrinkSelected.openState[currentDrinkSelected.state])
                {
                    openbutton.Content = "Закрыть";
                    drinkGlassSprite.Source = currentDrinkSelected.GetSprite("open");
                    if (currentDrinkSelected.state == "cartons" || currentDrinkSelected.state == "bottles")
                        AudioManager.PlaySound(openbottle);
                    else
                        AudioManager.PlaySound("can_open" + rand.Next(1, 4) + ".wav");
                }
                else
                {
                    openbutton.Content = "Открыть";
                    drinkGlassSprite.Source = currentDrinkSelected.GetSprite("icon");
                    if (currentDrinkSelected.state == "cartons" || currentDrinkSelected.state == "bottles")
                        AudioManager.PlaySound(closebottle);
                }
            }
        }
        private void SolutionDone(TextBlock RecipeText, Drink reagentobj, double lastAmount, int recipejuckyou)
        {
            var old = RecipeText.Inlines.ToList();
            RecipeText.Inlines.Clear();
            for (int i = 0; i < old.Count; i++)
            {
                RecipeText.Inlines.Add(old[i]);
                if (i == recipejuckyou)
                {
                    Recipe inputinputlol = recipe[reagentobj];
                    double coficent = Math.Ceiling(lastAmount / inputinputlol.output);
                    int totalVolume = 0;
                    foreach (var reagent in inputinputlol.input)
                    {
                        if (allIdsDrinks.ContainsKey(reagent.Key))
                        {
                            totalVolume += (short)Math.Ceiling(coficent * reagent.Value);
                        }
                    }
                    switch (inputinputlol.action)
                    {
                        case "None":
                            RecipeText.Inlines.Add("Смешать.\n");
                            break;
                        case "Stir":
                            RecipeText.Inlines.Add("Стир.\n");
                            break;
                        case "Shake":
                            RecipeText.Inlines.Add("Встряхнуть.\n");
                            break;
                    }
                    if (totalVolume != Math.Ceiling(lastAmount))
                    {
                        Run drinkaction = new();
                        if (Math.Ceiling(lastAmount) % 5 == 0)
                        {
                            RecipeText.Inlines.Add("Cлить джигером ");
                            drinkaction = new((totalVolume - 5 * Math.Floor(Math.Ceiling(lastAmount) / 5f)).ToString());
                        }
                        else
                        {
                            drinkaction = new((totalVolume - Math.Ceiling(lastAmount)).ToString());
                            RecipeText.Inlines.Add("Взять пипеткой ");
                        }
                        RecipeText.Inlines.Add(drinkaction);
                        drinkaction.Foreground = (Brush)new BrushConverter().ConvertFromString(reagentobj.color)!;
                        RecipeText.Inlines.Add(".\n");
                    }
                }
            }
        }

        private void DrawOffset(Drink drink, int offset, bool skipFirstSet = true)
        {
            int totalindex = drink.index + offset;
            if (totalindex >= 0 && totalindex < allDrinks.Count)
            {
                AudioManager.PlaySound(clickFilterButtonSound);
                if (inArrow)
                    skipFirst = skipFirstSet;
                drinksHistory = [];
                ResetSpriteTimer();
                amount = 30f;
                DrawDrink(allDrinks[totalindex]);
            }
            else
            {
                AudioManager.PlaySound(errSound);
            }
        }
        private void BuildHyperlink(TextBlock RecipeText, Drink drink, double amount, double lastAmount, Drink reagentobj, string text)
        {
            Run pretext = new("Добавить ");
            Hyperlink hyperlink = new();
            hyperlink.Click += Hyperlink_Click;
            hyperlink.TextDecorations = null;
            Run run = new(text);
            Run newline = new(",\n");
            hyperlink.Inlines.Add(run);
            var old = RecipeText.Inlines.ToList();
            RecipeText.Inlines.Clear();
            for (int i = 0; i < old.Count; i++)
            {
                RecipeText.Inlines.Add(old[i]);
                if (i == 0)
                {
                    RecipeText.Inlines.Add(pretext);
                    RecipeText.Inlines.Add(hyperlink);
                    RecipeText.Inlines.Add(newline);
                }
            }
            hyperlink.MouseRightButtonDown += (s, e) =>
            {
                if (recipe.TryGetValue(reagentobj, out Recipe? inputinputlol))
                {
                    AudioManager.PlaySound(yeahSound);
                    int totalVolume = 0;
                    RecipeText.Inlines.Remove(newline);
                    RecipeText.Inlines.Remove(pretext);
                    RecipeText.Inlines.Remove(hyperlink);
                    int index = 0;
                    double coficent = Math.Ceiling(lastAmount / inputinputlol.output);
                    foreach (var reagent in inputinputlol.input)
                    {
                        if (allIdsDrinks.TryGetValue(reagent.Key, out Drink? reagentobj))
                        {
                            index++;
                            short toadd = (short)Math.Ceiling(coficent * reagent.Value);
                            reagentobj.hyperlinkson = true;
                            totalVolume += toadd;
                            BuildHyperlink(RecipeText, drink, lastAmount, coficent * reagent.Value, reagentobj, GetName(reagentobj) + " (" + toadd.ToString(CultureInfo.InvariantCulture) + ")");
                        }
                        else
                        {
                            Debug.WriteLine(reagent.Key + " MISSING");
                            RecipeText.Inlines.Add("ой☺\n");
                        }
                    }
                    SolutionDone(RecipeText, reagentobj, lastAmount, index * 3);
                }
            };
            hyperlink.MouseEnter += (s, e) =>
            {
                AudioManager.PlaySound(hoverSound);
            };
            History historyobj = new(reagentobj);
            hyperlink.Tag = historyobj;
            historyobj.parentHistory = drink;
            historyobj.currentAmount = (float)amount;
            historyobj.lastAmount = lastAmount;
            run.Foreground = (Brush)new BrushConverter().ConvertFromString(reagentobj.color)!;
        }
        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            AudioManager.PlaySound(clickSubFilterButtonSound);
            History hystoryobj = (History)((Hyperlink)sender).Tag;
            Drink drink = hystoryobj.drink;
            float tmp = amount;
            if (drink.hyperlinkson)
            {
                drinksHistory.Add(hystoryobj.parentHistory, tmp);
            }
            else
            {
                drinksHistory.Add(hystoryobj.parentHistory, hystoryobj.currentAmount);
            }
            amount = (float)Math.Ceiling(hystoryobj.lastAmount);
            ResetSpriteTimer();
            DrawDrink(drink);
        }

        private void Subfilterclick(object sender, RoutedEventArgs e)
        {
            AudioManager.PlaySound(clickSubFilterButtonSound);
            amount = 30f;
            DrawDrink((Drink)((Button)sender).Tag);
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Mouse.LeftButton == MouseButtonState.Pressed)
                this.DragMove();
        }

        private bool topmosttoggle = false;
        private void Windowkeydown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.H)
            {
                System.Windows.Controls.Image tmp = new()
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Source = jokerge,
                    Width = 300
                };
                MainGrid.Children.Add(tmp);
                AudioManager.PlaySound("bikehorn.wav", () =>
                {
                    MainGrid.Children.Remove(tmp);
                });
            }
            else if (e.Key == Key.T)
            {
                topmosttoggle = !topmosttoggle;
                this.Topmost = topmosttoggle;
            }
            else if (e.Key == Key.Escape && filtersection != 0)
            {
                ((Button)MainGrid.Children[selected]).Background = Brushes.White;
                ((Button)MainGrid.Children[selected]).Foreground = Brushes.Black;
                selected = 0;
                secondGrid.Children.Clear();
                IntroImage.Opacity = 1;
                filtersection = 0;
                e.Handled = true;
                AudioManager.PlaySound("logshow.wav");
            }
            else if (filtersection == 3)
            {
                if (e.Key == Key.Z)
                {
                    string strresult = "";
                    strresult += "name: " + currentDrinkSelected.name;
                    strresult += "\nid: " + currentDrinkSelected.id;
                    strresult += "\nsprite: " + currentDrinkSelected.sprite;
                    strresult += "\ncolor: " + currentDrinkSelected.color;
                    strresult += "\ndesc: " + currentDrinkSelected.name.Replace("name", "description");
                    strresult += "\nphysdesc: " + currentDrinkSelected.physicalDesc;
                    strresult += "\nflavor: " + currentDrinkSelected.flavor;
                    strresult += "\nethanol: " + currentDrinkSelected.Ethanol.ToString();
                    strresult += "\nlocal_index: " + currentDrinkSelected.index.ToString();
                    strresult += "\nmaxFillness: " + currentDrinkSelected.maxLevels[currentDrinkSelected.state].ToString();
                    strresult += "\nfillness: " + currentDrinkSelected.fill[currentDrinkSelected.state].ToString();
                    strresult += "\nsatiateThirst: " + currentDrinkSelected.SatiateThirst.ToString();
                    strresult += "\npoison: " + currentDrinkSelected.Poison.ToString();
                    strresult += "\nstate: " + currentDrinkSelected.state;
                    if (recipe.TryGetValue(currentDrinkSelected, out Recipe? value))
                    {
                        strresult += "\ninput:\n";
                        foreach (var idk in value.input)
                        {
                            strresult += "  " + idk.Key + ": " + idk.Value.ToString() + "\n";
                        }
                        strresult += "output: " + value.output.ToString();
                    }
                    else
                    {
                        strresult += "\nno recipe";
                    }
                    AudioManager.PlaySound("bleep_config.wav");
                    MessageBox.Show(strresult,"data");
                    ;
                }
                else if (e.Key == Key.Left || e.Key == Key.A)
                    DrawOffset(currentDrinkSelected, -1);
                else if (e.Key == Key.Right || e.Key == Key.D)
                    DrawOffset(currentDrinkSelected, 1);
                else if (e.Key == Key.Up || e.Key == Key.W)
                    ChangeFillnes(1);
                else if (e.Key == Key.Down || e.Key == Key.S)
                    ChangeFillnes(-1);
                else if (e.Key == Key.E)
                    ToggleContaier();
                else if (e.Key == Key.X)
                    BottleToggle();
            }
        }

    }
}