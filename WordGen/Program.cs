using Microsoft.VisualBasic.FileIO;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using static MoreLinq.Extensions.BatchExtension;

Console.WriteLine("Hello, World!");

//var existingWords = WordGenLib.GridReader.AllowedWords();
//HttpClientHandler handler = new();
//handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
//HttpClient client = new(handler);

Regex hasNumber = new("[0-9]");

File.WriteAllLines("../../../flashcards2.txt",
File.ReadAllLines("../../../flashcards2.txt")
    .Where(sentence => !hasNumber.IsMatch(sentence))
    .Where(sentence => sentence.Length <= 25)
    .Where(sentence => sentence.Length >= 3)
    .Select(sentence => sentence.Replace('-', ' '))
    .Except(File.ReadAllLines("../../../flashcards.txt"))
    .DistinctBy(sentence => sentence.Replace(" ", ""))
    .OrderBy(sentence=>sentence)
    );

//File.WriteAllLines("../../../flashcards.txt", defs.Select(s => s.ToLower()).Distinct());


//File.WriteAllLines("../../../../WordGenLib/from-lexems.txt",
//File.ReadAllLines("../../../../WordGenLib/from-lexems.txt")
//    .Except(common)
//    );

    //var wikiapi = @"https://en.wikipedia.org/w/api.php?action=query&format=json&list=search&utf8=1&formatversion=2&srnamespace=0&srlimit=1&srwhat=text&srinfo=totalhits&srsearch=";
////var wikiapi = @"https://www.wikidata.org/w/api.php?action=query&format=json&list=search&utf8=1&formatversion=2&srnamespace=0&srlimit=1&srwhat=text&srinfo=totalhits&srsearch=";

////File.WriteAllLines("../../../../WordGenLib/words.txt",
//var filterd =
//    obscure
//        //.Take(21153)
//        //.Concat(
//        //    allWords.Skip(21153)
//        .Select((x, idx) =>
//        {
//            if (idx % 1000 == 0) Console.WriteLine($"[mto] {idx} - {x}...");
//            return x;
//        })
//        .Batch(20)
//        .SelectMany(batch =>
//        {
//            //var keep = batch.Where(word => word.Length > 6);

//            var filter = batch
//                //.Where(title => title.Length <= 6)
//                .Select(title => (title, client.GetAsync(wikiapi + Uri.EscapeDataString(title))))
//                .Select(async tuple =>
//                {
//                    var title = tuple.title;
//                    var response = await tuple.Item2;

//                    using Stream stream = response.Content.ReadAsStream();
//                    using StreamReader reader = new(stream);

//                    var jsonString = reader.ReadToEnd();
//                    var json = JsonNode.Parse(jsonString);

//                    var totalHits_ = json?["query"]?["searchinfo"]?["totalhits"];
//                    if (totalHits_ == null) throw new Exception("Unexpected total hits null in " + jsonString);
//                    var totalHits = (int)totalHits_.AsValue();

//                    //var snippet = ( ( (string?) json?["query"]?["search"]?[0]?["snippet"]?.AsValue() ) ?? "" ).ToLower();
//                    //var isPlace = snippet.Contains("town") || snippet.Contains("river") || snippet.Contains("road") || snippet.Contains("street") || snippet.Contains("city");
//                    bool hasSpace = title.Contains(' ');

//                    //bool include = (!isPlace && totalHits > 500) || totalHits > 5000;
//                    bool include = totalHits > 10000;

//                    return (title, include);
//                });
//            var resultsAsync = Task.WhenAll(filter);
//            resultsAsync.Wait();

//            return resultsAsync.Result
//                .Where(tuple => tuple.include)
//                .Select(tuple => tuple.title)
//                //.Concat(keep)
//                .OrderBy(word => word);
//        })
//        .Select((x, idx) =>
//        {
//            if (idx % 1000 == 0) Console.WriteLine($"\t\t\t\t{idx} written so far. {x}");
//            return x;
//        });
//    //.Concat(allWords.Skip(21153))
//    //)
//    //);
//    ;


int gridSize = 5;  // 10 x 10
var generator = WordGenLib.Generator.Create(gridSize);
var grids = generator.PossibleGrids().Take(5).Zip(Enumerable.Range(0, 5));


foreach (var (grid, idx) in grids)
{
    Console.WriteLine($"\nGrid {idx} / 5: \n");
    Console.WriteLine(grid);
    Console.WriteLine();
}