using System;
using System.IO;
using System.Text.RegularExpressions;

class Program
{
    static void Main()
    {
        string[] files = {
            @"e:\CarRentalSystem\CarRentalSystem\CarRentalSystem\docs\CarRentalSystem_FDD_VisioReady.drawio",
            @"e:\CarRentalSystem\CarRentalSystem\CarRentalSystem\docs\CarRentalSystem_FDD_Template.drawio",
            @"e:\CarRentalSystem\CarRentalSystem\CarRentalSystem\docs\CarRentalSystem_FDD.drawio"
        };

        foreach (var file in files)
        {
            string content = File.ReadAllText(file);
            string newContent = Regex.Replace(content, @"(value=")([^""]*)""", m =>
            {
                return m.Groups[1].Value + RemoveDiacritics(m.Groups[2].Value) + "\"";
            });
            File.WriteAllText(file, newContent);
            Console.WriteLine("Processed: " + file);
        }
        Console.WriteLine("Done!");
    }

    static string RemoveDiacritics(string text)
    {
        string[] diacritics = { "à","á","ả","ã","ạ","ằ","ắ","ẳ","ẵ","ặ","ầ","ấ","ẩ","ẫ","ậ","è","é","ẻ","ẽ","ẹ","ề","ế","ể","ễ","ệ","ì","í","ỉ","ĩ","ị","ò","ó","ỏ","õ","ọ","ồ","ố","ổ","ỗ","ộ","ờ","ớ","ở","ỡ","ợ","ù","ú","ủ","ũ","ụ","ừ","ứ","ử","ữ","ự","ỳ","ý","ỷ","ỹ","ỵ","đ","À","Á","Ả","Ã","Ạ","Ằ","Ắ","Ẳ","Ẵ","Ặ","Ầ","Ấ","Ẩ","Ẫ","Ậ","È","É","Ẻ","Ẽ","Ẹ","Ề","Ế","Ể","Ễ","Ệ","Ì","Í","Ỉ","Ĩ","Ị","Ò","Ó","Ỏ","Õ","Ọ","Ồ","Ố","Ổ","Ỗ","Ộ","Ờ","Ớ","Ở","Ỡ","Ợ","Ù","Ú","Ủ","Ũ","Ụ","Ừ","Ứ","Ử","Ữ","Ự","Ỳ","Ý","Ỷ","Ỹ","Ỵ","Đ" };
        string[] replacements = { "a","a","a","a","a","ă","ă","ă","ă","ă","â","â","â","â","â","e","e","e","e","e","ê","ê","ê","ê","ê","i","i","i","i","i","o","o","o","o","o","ô","ô","ô","ô","ô","ơ","ơ","ơ","ơ","ơ","u","u","u","u","u","ư","ư","ư","ư","ư","y","y","y","y","y","d","A","A","A","A","A","Ă","Ă","Ă","Ă","Ă","Â","Â","Â","Â","Â","E","E","E","E","E","Ê","Ê","Ê","Ê","Ê","I","I","I","I","I","O","O","O","O","O","Ô","Ô","Ô","Ô","Ô","Ơ","Ơ","Ơ","Ơ","Ơ","U","U","U","U","U","Ư","Ư","Ư","Ư","Ư","Y","Y","Y","Y","Y","D" };

        for (int i = 0; i < diacritics.Length; i++)
            text = text.Replace(diacritics[i], replacements[i]);

        return text;
    }
}
