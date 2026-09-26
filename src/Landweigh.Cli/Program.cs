var filePath = args.Length > 0 ? args[0] : throw new ArgumentException("File path argument is required.");
//read the file and call ListingPruner.Prune on the contents, then print the result to the console
var html = await File.ReadAllTextAsync(filePath);
//print the original length of the file and the pruned length
Console.WriteLine($"Original length: {html.Length}");
//after pruning, print the length of the pruned text
var pruned = Landweigh.Core.ListingPruner.Prune(html);
Console.WriteLine($"Pruned length: {pruned.Length}");

