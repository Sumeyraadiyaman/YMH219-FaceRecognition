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
