using System.Collections;
using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration.Attributes;
using SixLabors.Fonts;
using SixLabors.Fonts.Unicode;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.CommandLine;

var inputOption = new Option<FileInfo>("--input", "Kotoba bot CSV file to base quiz on.")
{
    IsRequired = true,
};
var fontOption = new Option<FileInfo>("--font", "The font to use.")
{
    IsRequired = true,
};
var urlTemplateOption =
    new Option<string>("--url-template", "The template to use for the Question URLs. Use {0} for the filename.")
    {
        IsRequired = true,
    };
var fontSizeOption = new Option<int>("--font-size", () => 400, "The font size to draw with.");
var resultOption = new Option<DirectoryInfo?>("--result",
    () => Directory.Exists("./result") ? new DirectoryInfo("./result") : null,
    "The directory to place the resulting images and json file in. Default is ./result.");
var deleteResultOption =
    new Option<bool>("--delete-result", () => true, "Whether or not to delete the result directory.");
var instructionsOption = new Option<string?>("--instructions", () => null,
    "The instructions to set to each question. If this option is not set or is set to \"null\" instructions will be wiped.");
var rotateOption = new Option<float>("--rotate", () => -1f,
    "How many degrees to rotate each character by. If not set or set to a negative number rotation will be random.");
var cpusOption = new Option<int>("--cpus", () => Environment.ProcessorCount, "The number of threads to use for generating images. By default your number of cores is used.");
var generateImagesOption = new Option<bool>("--generate-images", () => true);

var rootCommand = new RootCommand
{
    inputOption,
    fontOption,
    urlTemplateOption,
    fontSizeOption,
    resultOption,
    deleteResultOption,
    instructionsOption,
    rotateOption,
    cpusOption,
    generateImagesOption,
};
rootCommand.SetHandler(async c =>
    {
        var input = c.ParseResult.GetValueForOption(inputOption)!;
        var fontFile = c.ParseResult.GetValueForOption(fontOption)!;
        var urlTemplate = c.ParseResult.GetValueForOption(urlTemplateOption)!;
        var fontSize = c.ParseResult.GetValueForOption(fontSizeOption);
        var result = c.ParseResult.GetValueForOption(resultOption);
        var deleteResult = c.ParseResult.GetValueForOption(deleteResultOption);
        var instructions = c.ParseResult.GetValueForOption(instructionsOption);
        var rotate = c.ParseResult.GetValueForOption(rotateOption);
        var cpus = c.ParseResult.GetValueForOption(cpusOption);
        var generateImages = c.ParseResult.GetValueForOption(generateImagesOption);
        
        result ??= new DirectoryInfo("./result");
        var imagesPath = Path.Combine(result.FullName, "images");
        if (result.Exists && deleteResult)
            Directory.Delete(result.FullName, true);
        if (!Directory.Exists(result.FullName))
            Directory.CreateDirectory(result.FullName);
        if (!Directory.Exists(imagesPath))
            Directory.CreateDirectory(imagesPath);

        if (instructions is "null" or null)
            instructions = "";

        var rng = new Random(1);
        var fonts = new FontCollection();
        var font1 = fonts.Add(fontFile.FullName);
        var font = new Font(font1, fontSize);

        using var reader = new StreamReader(input.FullName);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        var records = csv.GetRecords<FullEntry>().ToArray();
        var numberDone = 0;
        var locker = new object();
        var entries = new List<FullEntry>(records.Length);
        await Parallel.ForEachAsync(records, new ParallelOptions
        {
            MaxDegreeOfParallelism = cpus,
        }, async (entry, _) =>
        {
            var filename = $"{entry.Question}.webp";
            if (generateImages)
                await MakeTextWebp(entry.Question, Path.Combine(result.FullName, "images", filename));
            entry.Question = string.Format(urlTemplate, filename);
            entry.Instructions = instructions;
            entry.RenderAs = "Image URI";
            entries.Add(entry);
            lock (locker)
            {
                Console.WriteLine($"Done {++numberDone}/{records.Length}");
            }
        });


        await using var writer = new StreamWriter(Path.Combine(result.FullName, "output.csv"));
        await using var csvWrite = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csvWrite.WriteRecords((IEnumerable)entries);
        return;

        async Task MakeTextWebp(string text, string filename)
        {
            var height = 0;

            var utf32 = Encoding.UTF32.GetBytes(text);
            for (var i = 0; i < utf32.Length; i += 4)
            {
                if (!font.TryGetGlyphs(new CodePoint(BitConverter.ToInt32(utf32, i)), ColorFontSupport.None,
                        out var glyphs))
                    throw new($"Glyph not found: {Encoding.UTF32.GetString(utf32[i..(i + 4)])}");
                var g = glyphs[0];
                var glyphHeight = g.GlyphMetrics.AdvanceHeight * (fontSize / 750d);
                if (glyphHeight > height) height = (int)glyphHeight;
            }

            var images = new List<Image<Argb32>>();
            for (var i = 0; i < utf32.Length; i += 4)
            {
                var image2 = new Image<Argb32>(fontSize, height);
                image2.Mutate(c =>
                {
                    c.DrawText(Encoding.UTF32.GetString(utf32.AsSpan()[i..(i + 4)]), font, Color.White,
                        new PointF(0, 0));
                    var angle = rotate < 0 ? rng.NextSingle() * 360f : rotate;
                    c.Rotate(angle);
                });
                images.Add(image2);
            }

            var width = 0;

            foreach (var image2 in images)
            {
                width += image2.Width;
                if (image2.Height > height) height = image2.Height;
            }

            Console.WriteLine($"text: '{text}'; width: {width}; height: {height};");
            if (width > 16384 || height > 16384)
                throw new("Width or height is larger than 16384, the maximum for the WebP format. Use a smaller font size!");
            using var image = new Image<Argb32>(width, height);
            var x = 0;
            foreach (var image2 in images)
            {
                image.Mutate(
                    c => c.DrawImage(image2, new Point(x, 0), new Rectangle(0, 0, image2.Width, image2.Height), 1f));
                x += image2.Width;
                image2.Dispose();
            }

            await image.SaveAsWebpAsync(filename);
        }
    });

return await rootCommand.InvokeAsync(args);

public class FullEntry
{
    public string Question { get; set; }
    public string Answers { get; set; }
    public string Comment { get; set; }
    public string Instructions { get; set; }
    [Name("Render as")] public string RenderAs { get; set; }
}
