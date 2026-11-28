using OpenCvSharp;

namespace YMH219_FaceRecognition
{
    // Görüntü okuma/yazma ve yüz görüntüsünü hazırlama ile ilgili yardımcı metotlar
    public static class FaceImage
    {
        // Tanıma için bütün yüzler aynı boyuta getirilir
        public const int FaceSize = 100;

        // Gri görüntüden yüz bölgesini kesip 100x100 boyutuna getirir ve ışığı dengeler
        public static Mat PrepareFace(Mat gray, Rect area)
        {
            using Mat region = new Mat(gray, area);
            Mat face = new Mat();
            Cv2.Resize(region, face, new OpenCvSharp.Size(FaceSize, FaceSize));
            Cv2.EqualizeHist(face, face); // Karanlık/aydınlık farkını azaltır
            return face;
        }

        // Gizlilik modu: yüz bölgesini pikselleştirir (önce çok küçültüp sonra büyütür, ayrıntı kaybolur)
        public static void Pixelate(Mat image, Rect area)
        {
            // Dikdörtgeni görüntünün sınırları içinde tut
            int x = Math.Max(0, area.X);
            int y = Math.Max(0, area.Y);
            int width = Math.Min(area.Width, image.Width - x);
            int height = Math.Min(area.Height, image.Height - y);
            if (width <= 0 || height <= 0)
                return;

            using Mat region = new Mat(image, new Rect(x, y, width, height)); // Orijinal görüntünün o bölgesi
            using Mat small = new Mat();
            using Mat blocky = new Mat();

            Cv2.Resize(region, small, new OpenCvSharp.Size(10, 10), 0, 0, InterpolationFlags.Area);
            Cv2.Resize(small, blocky, new OpenCvSharp.Size(width, height), 0, 0, InterpolationFlags.Nearest);
            blocky.CopyTo(region); // Bölgeye yazmak, orijinal görüntüyü değiştirir
        }

        // Dosyadan görüntü okur.
        // Not: OpenCV'nin kendi ImRead metodu Türkçe karakterli dosya yollarında (ör. "Sümeyra") çalışmaz.
        // Bu yüzden dosyayı önce C# ile byte olarak okuyup sonra OpenCV'ye çözdürüyoruz.
        public static Mat Load(string path, ImreadModes mode = ImreadModes.Color)
        {
            byte[] bytes = File.ReadAllBytes(path);
            Mat image = Cv2.ImDecode(bytes, mode);

            if (image.Empty())
            {
                image.Dispose();
                throw new InvalidDataException("Dosya bir görüntü olarak okunamadı: " + Path.GetFileName(path));
            }

            return image;
        }

        // Görüntüyü PNG olarak kaydeder (Türkçe karakterli yollarda da çalışır)
        public static void SavePng(string path, Mat image)
        {
            Cv2.ImEncode(".png", image, out byte[] bytes);
            File.WriteAllBytes(path, bytes);
        }
    }
}
