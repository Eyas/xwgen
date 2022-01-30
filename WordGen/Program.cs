using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static MoreLinq.Extensions.BatchExtension;

Console.WriteLine("Hello, World!");

//var existingWords = WordGenLib.GridReader.AllowedWords();
HttpClientHandler handler = new();
handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
HttpClient client = new(handler);

var allWords = File.ReadAllLines("../../../../WordGenLib/from-lexems.txt");

var wikiapi  = @"https://en.wikipedia.org/w/api.php?action=query&format=json&list=search&utf8=1&formatversion=2&srnamespace=0&srlimit=1&srwhat=text&srinfo=totalhits&srsearch=";
File.WriteAllLines("../../../../WordGenLib/from-lexems.txt",
    allWords
    .Select((x, idx) =>
    {
        if (idx % 1000 == 0) Console.WriteLine($"{idx} - {x}...");
        return x;
    })
    .Batch(25)
    .SelectMany(batch =>
    {
        var keep = batch
        .Where(title => title.Contains(' ') || title.Length > 20);

        var filter = batch
            .Where(title => !title.Contains(' ') && title.Length <= 20)
            .Select(title => (title, client.GetAsync(wikiapi + Uri.EscapeDataString(title))))
            .Select(async tuple =>
            {
                var title = tuple.title;
                var response = await tuple.Item2;

                using Stream stream = response.Content.ReadAsStream();
                using StreamReader reader = new(stream);

                var jsonString = reader.ReadToEnd();
                var json = JsonNode.Parse(jsonString);

                var totalHits = json?["query"]?["searchinfo"]?["totalhits"];
                if (totalHits == null) throw new Exception("Unexpected total hits null in " + jsonString);

                return (title, totalHits);
            });
        var resultsAsync = Task.WhenAll(filter);
        resultsAsync.Wait();

        return resultsAsync.Result
            .Where(tuple => ((int)tuple.totalHits.AsValue()) > 50)
            .Select(tuple => tuple.title)
            .Concat(keep)
            .OrderBy(word => word);
    })
    .Select((x, idx) =>
    {
        if (idx % 1000 == 0) Console.WriteLine($"\t\t\t\t{idx} written so far. {x}");
        return x;
    })
    );
//File.WriteAllLines("../../../wikipedia2.txt",
//    File.ReadLines("../../../wikipedia.txt", Encoding.UTF8)
//    .Where(title =>
//    {
//        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(wikiapi + Uri.EscapeDataString(title));
//        request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

//        using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
//        using (Stream stream = response.GetResponseStream())
//        using (StreamReader reader = new StreamReader(stream))
//        {
//            var jsonString = reader.ReadToEnd();
//            var json = JsonNode.Parse(jsonString);
//            var pageNode = json?["query"]?["pages"]?.AsArray()?[0];

//            if (pageNode == null) throw new Exception("JSON page node null in response\n" + jsonString + "\nrequest\n" + title);

//            var links = pageNode?["linkshere"]?.AsArray();
//            if (links == null) return false;
//            return links.Count >= 25;
//        }
//    }));

int gridSize = 10;  // 10 x 10

var generator = WordGenLib.Generator.Create(gridSize);
var grid = generator.GenerateGrid().First();


for (int i = 0; i < gridSize; ++i)
{
    for (int j = 0; j < gridSize; ++j)
    {
        char cell = grid[i, j] switch
        {
            null => '_',
            char v => v,
        };

        Console.Write(cell);
        Console.Write(' ');
    }
    Console.WriteLine();
}
