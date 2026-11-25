using System.Diagnostics;
using System.Drawing.Imaging;
using OpenCvSharp.Extensions;
using Mat = OpenCvSharp.Mat;

namespace YMH219_FaceRecognition
{
    // Ana pencere: kamera/görüntü gösterimi, yüz algılama ve tanıma, örnek ekleme ve eğitim.
    // Arayüz kodla oluşturulmuştur (BuildUi metodu).
    public class MainForm : Form
    {
        // Pembe - mint renk paleti
        private static readonly Color PinkColor = Color.FromArgb(236, 64, 122);
        private static readonly Color MintColor = Color.FromArgb(38, 166, 154);
        private static readonly Color LightMintColor = Color.FromArgb(77, 182, 172);
        private static readonly Color RedColor = Color.FromArgb(239, 83, 80);
        private static readonly Color LavenderColor = Color.FromArgb(171, 130, 220);
        private static readonly Color GrayColor = Color.FromArgb(176, 176, 176);
        private static readonly Color DisabledColor = Color.FromArgb(215, 215, 215);
        private static readonly Color TextColor = Color.FromArgb(55, 45, 50);

        private readonly CameraService camera = new CameraService();
        private readonly FaceRecognizerService recognizer = new FaceRecognizerService();
        private readonly FaceDataset dataset;
        private readonly string capturesFolder;
        private FaceDetector? detector;
        private FaceAnalyzer? analyzer;

        private int frameBusy = 0;      // 1 = önceki kare henüz ekrana basılmadı (yeni kare atlanır)
        private bool isTraining = false;

        private PictureBox picPreview = null!;
        private Button btnCameraStart = null!;
        private Button btnCameraStop = null!;
        private Button btnSelectImage = null!;
        private Button btnSaveFrame = null!;
        private TextBox txtLabel = null!;
        private Button btnAddSample = null!;
        private Button btnTrain = null!;
        private ListBox lstLabels = null!;
        private Button btnOpenFolder = null!;
        private Label lblResult = null!;
        private Label lblStatus = null!;

        public MainForm()
        {
            BuildUi();

            // Veriler Belgeler klasöründe tutulur (proje klasörüne ve GitHub'a gitmez)
            string appFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "YMH219-FaceRecognition");
            dataset = new FaceDataset(Path.Combine(appFolder, "Yuzler"));
            capturesFolder = Path.Combine(appFolder, "Kayitlar");

            camera.FrameCaptured += Camera_FrameCaptured;
            camera.CameraError += Camera_CameraError;

            btnCameraStart.Click += BtnCameraStart_Click;
            btnCameraStop.Click += BtnCameraStop_Click;
            btnSelectImage.Click += BtnSelectImage_Click;
            btnSaveFrame.Click += BtnSaveFrame_Click;
            btnAddSample.Click += BtnAddSample_Click;
            btnTrain.Click += async (s, e) => await TrainAsync(showMessages: true);
            btnOpenFolder.Click += BtnOpenFolder_Click;
            Shown += MainForm_Shown;
            FormClosing += MainForm_FormClosing;
        }

        #region Açılış ve kapanış

        private async void MainForm_Shown(object? sender, EventArgs e)
        {
            LoadDetector();
            RefreshLabelList();
            UpdateControls();

            // Daha önce kaydedilmiş örnekler varsa modeli otomatik eğit
            if (detector != null && dataset.GetSampleCounts().Count(kv => kv.Value > 0) >= 2)
                await TrainAsync(showMessages: false);
        }

        // Haar yüz algılama modelini yükler
        private void LoadDetector()
        {
            string cascadePath = Path.Combine(Application.StartupPath, "Models", "haarcascade_frontalface_default.xml");

            try
            {
                detector = new FaceDetector(cascadePath);
                analyzer = new FaceAnalyzer(detector, recognizer);
                SetStatus("Hazır. Kamerayı başlatın veya bir görüntü seçin.", TextColor);
            }
            catch (Exception ex)
            {
                SetStatus("Yüz algılama modeli yüklenemedi", RedColor);
                MessageBox.Show(
                    $"Yüz algılama modeli yüklenemedi.\n\nBeklenen konum:\n{cascadePath}\n\nAyrıntı: {ex.Message}",
                    "Model bulunamadı", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            camera.FrameCaptured -= Camera_FrameCaptured;
            camera.CameraError -= Camera_CameraError;
            camera.Dispose();

            analyzer?.Dispose();
            detector?.Dispose();
            recognizer.Dispose();
            picPreview.Image?.Dispose();
        }

        #endregion

        #region Kamera ve görüntü

        private void BtnCameraStart_Click(object? sender, EventArgs e)
        {
            try
            {
                camera.Start();
                SetStatus("Kamera açık. Yüzler algılanıyor...", MintColor);
            }
            catch (Exception ex)
            {
                SetStatus("Kamera açılamadı", RedColor);
                MessageBox.Show(ex.Message, "Kamera hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            UpdateControls();
        }

        private void BtnCameraStop_Click(object? sender, EventArgs e)
        {
            camera.Stop();
            SetStatus("Kamera durduruldu.", TextColor);
            UpdateControls();
        }

        // Kamera koptu (arka plan iş parçacığından gelir)
        private void Camera_CameraError(string message)
        {
            TryRunOnUi(() =>
            {
                camera.Stop();
                SetStatus("Kamera bağlantısı kesildi", RedColor);
                UpdateControls();
                MessageBox.Show(message, "Kamera hatası", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            });
        }

        // Kameradan yeni kare geldi (arka plan iş parçacığından gelir)
        private void Camera_FrameCaptured(Mat frame)
        {
            if (analyzer == null)
                return;

            // Önceki kare henüz ekrana basılmadıysa bu kareyi atla (birikme olmasın)
            if (Interlocked.Exchange(ref frameBusy, 1) == 1)
                return;

            Bitmap? bitmap = null;
            try
            {
                List<DetectedFace> faces = analyzer.Analyze(frame);
                bitmap = BitmapConverter.ToBitmap(frame);
                DrawFaces(bitmap, faces);

                Bitmap ready = bitmap;
                bitmap = null;

                bool queued = TryRunOnUi(() =>
                {
                    ShowImage(ready, faces);
                    Interlocked.Exchange(ref frameBusy, 0);
                });

                if (!queued)
                {
                    ready.Dispose();
                    Interlocked.Exchange(ref frameBusy, 0);
                }
            }
            catch (Exception)
            {
                // Tek bir karede hata olursa sıradaki kareyle devam et
                bitmap?.Dispose();
                Interlocked.Exchange(ref frameBusy, 0);
            }
        }

        // Bilgisayardan bir resim seçip içindeki yüzleri bulur ve tanır
        private void BtnSelectImage_Click(object? sender, EventArgs e)
        {
            if (analyzer == null)
                return;

            using OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "Bir görüntü seçin",
                Filter = "Görüntü dosyaları (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp"
            };

            if (dialog.ShowDialog() != DialogResult.OK)
                return;

            if (camera.IsRunning)
                camera.Stop(); // Kamera görüntüsü seçilen resmin üstüne yazmasın

            try
            {
                using Mat image = FaceImage.Load(dialog.FileName);
                List<DetectedFace> faces = analyzer.Analyze(image);

                Bitmap bitmap = BitmapConverter.ToBitmap(image);
                DrawFaces(bitmap, faces);
                ShowImage(bitmap, faces);

                SetStatus($"Görüntü: {Path.GetFileName(dialog.FileName)}", TextColor);
            }
            catch (Exception ex)
            {
                SetStatus("Görüntü açılamadı", RedColor);
                MessageBox.Show("Görüntü açılamadı.\n\nAyrıntı: " + ex.Message, "Hata",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            UpdateControls();
        }

        // Ekrandaki işaretli kareyi .png olarak kaydeder
        private void BtnSaveFrame_Click(object? sender, EventArgs e)
        {
            if (picPreview.Image == null)
                return;

            try
            {
                Directory.CreateDirectory(capturesFolder);
                string path = Path.Combine(capturesFolder, $"kare_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                picPreview.Image.Save(path, ImageFormat.Png);
                SetStatus("Kare kaydedildi: " + path, MintColor);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Kare kaydedilemedi: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Yeni görüntüyü PictureBox'a koyar, eskisini bellekten siler
        private void ShowImage(Bitmap bitmap, List<DetectedFace> faces)
        {
            Image? old = picPreview.Image;
            picPreview.Image = bitmap;
            old?.Dispose();

            ShowResults(faces);
            btnSaveFrame.Enabled = true;
        }

        // Yüzlerin etrafına dikdörtgen ve etiket çizer (Türkçe karakterler için GDI+ kullanılır)
        private static void DrawFaces(Bitmap bitmap, List<DetectedFace> faces)
        {
            using Graphics g = Graphics.FromImage(bitmap);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            float lineWidth = Math.Max(2, bitmap.Width / 220f);
            using Font font = new Font("Segoe UI", Math.Max(10, bitmap.Width / 50f), FontStyle.Bold);

            foreach (DetectedFace face in faces)
            {
                Color color = GetFaceColor(face.Name);
                Rectangle box = new Rectangle(face.Area.X, face.Area.Y, face.Area.Width, face.Area.Height);

                using Pen pen = new Pen(color, lineWidth);
                g.DrawRectangle(pen, box);

                string text = face.Distance > 0
                    ? $"{face.Name}  %{ConfidencePercent(face.Distance)}"
                    : face.Name;

                SizeF textSize = g.MeasureString(text, font);
                float textY = Math.Max(0, box.Y - textSize.Height - 2);

                using SolidBrush background = new SolidBrush(color);
                g.FillRectangle(background, box.X, textY, textSize.Width + 6, textSize.Height);
                g.DrawString(text, font, Brushes.White, box.X + 3, textY);
            }
        }

        // Sağdaki sonuç kutusunu günceller
        private void ShowResults(List<DetectedFace> faces)
        {
            if (faces.Count == 0)
            {
                lblResult.Text = "Yüz bulunamadı.";
                return;
            }

            List<string> lines = new List<string> { $"{faces.Count} yüz algılandı:" };
            for (int i = 0; i < faces.Count; i++)
            {
                DetectedFace face = faces[i];
                lines.Add(face.Distance > 0
                    ? $"{i + 1}. {face.Name}  —  güven %{ConfidencePercent(face.Distance)} (uzaklık {face.Distance:F1})"
                    : $"{i + 1}. {face.Name}");
            }

            lblResult.Text = string.Join(Environment.NewLine, lines);
        }

        // LBPH uzaklığını kabaca yüzdeye çevirir: 0 uzaklık = %100, 100 ve üstü = %0
        private static int ConfidencePercent(double distance)
        {
            return (int)Math.Round(Math.Clamp(100 - distance, 0, 100));
        }

        private static Color GetFaceColor(string name)
        {
            if (name == "Bilinmiyor")
                return RedColor;
            if (name == "Model eğitilmedi")
                return LavenderColor;
            return PinkColor;
        }

        #endregion

        #region Örnek ekleme ve eğitim

        // Ekrandaki en büyük yüzü yazılan etiketle veri kümesine ekler
        private void BtnAddSample_Click(object? sender, EventArgs e)
        {
            string label = txtLabel.Text.Trim();
            if (label.Length == 0)
            {
                MessageBox.Show("Lütfen önce bir etiket (isim) yazın.", "Etiket eksik",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtLabel.Focus();
                return;
            }

            using Mat? face = analyzer?.GetLastFace();
            if (face == null)
            {
                MessageBox.Show("Görüntüde yüz bulunamadı. Kameraya düz bakın veya yüz içeren bir resim seçin.",
                    "Yüz yok", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                dataset.AddSample(label, face);
                RefreshLabelList();

                int count = dataset.GetSampleCounts().TryGetValue(label, out int c) ? c : 0;
                SetStatus($"Örnek eklendi: {label} ({count} örnek). En az 10 örnek önerilir; sonra \"Modeli Eğit\"e basın.", MintColor);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Örnek kaydedilemedi: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Kayıtlı örneklerle modeli arka planda eğitir
        private async Task TrainAsync(bool showMessages)
        {
            if (isTraining)
                return;

            isTraining = true;
            UpdateControls();
            SetStatus("Model eğitiliyor...", Color.DimGray);

            try
            {
                int sampleCount = await Task.Run(() =>
                {
                    List<(string Label, Mat Face)> samples = dataset.LoadAllSamples();
                    try
                    {
                        recognizer.Train(samples);
                        return samples.Count;
                    }
                    finally
                    {
                        foreach (var sample in samples)
                            sample.Face.Dispose(); // Görüntüleri bellekten sil
                    }
                });

                SetStatus($"Model eğitildi ({sampleCount} örnek). Artık yüzler tanınıyor.", MintColor);
                if (showMessages)
                    MessageBox.Show($"Model {sampleCount} örnekle eğitildi.", "Eğitim tamamlandı",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (InvalidOperationException ex)
            {
                SetStatus("Model eğitilemedi: yeterli etiket yok", RedColor);
                if (showMessages)
                    MessageBox.Show(ex.Message, "Eğitim yapılamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                SetStatus("Model eğitilemedi", RedColor);
                if (showMessages)
                    MessageBox.Show("Eğitim sırasında hata oluştu: " + ex.Message, "Hata",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                isTraining = false;
                UpdateControls();
            }
        }

        private void BtnOpenFolder_Click(object? sender, EventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dataset.RootFolder}\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Klasör açılamadı: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Etiket listesini (isim ve örnek sayısı) yeniler
        private void RefreshLabelList()
        {
            lstLabels.Items.Clear();

            Dictionary<string, int> counts = dataset.GetSampleCounts();
            if (counts.Count == 0)
            {
                lstLabels.Items.Add("(Henüz örnek yok)");
                return;
            }

            foreach (KeyValuePair<string, int> item in counts.OrderBy(kv => kv.Key))
                lstLabels.Items.Add($"{item.Key}  —  {item.Value} örnek");
        }

        #endregion

        #region Yardımcı metotlar

        // Butonları duruma göre etkinleştirir
        private void UpdateControls()
        {
            bool ready = analyzer != null;
            bool running = camera.IsRunning;

            btnCameraStart.Enabled = ready && !running;
            btnCameraStop.Enabled = running;
            btnSelectImage.Enabled = ready;
            btnAddSample.Enabled = ready;
            btnTrain.Enabled = ready && !isTraining;
            btnSaveFrame.Enabled = picPreview.Image != null;
        }

        private void SetStatus(string text, Color color)
        {
            lblStatus.Text = "Durum: " + text;
            lblStatus.ForeColor = color;
        }

        // Arayüzü değiştiren kodu arayüz iş parçacığına gönderir; gönderemezse false döner
        private bool TryRunOnUi(Action action)
        {
            if (IsDisposed || !IsHandleCreated)
                return false;

            try
            {
                BeginInvoke(action);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false; // Pencere kapanıyor
            }
        }

        #endregion

        #region Arayüz

        private void BuildUi()
        {
            Text = "Yüz Algılama ve Tanıma";
            ClientSize = new Size(1000, 560);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(253, 246, 249);
            Font = new Font("Segoe UI", 9.5F);

            picPreview = new PictureBox
            {
                Location = new Point(15, 15),
                Size = new Size(660, 495),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(45, 38, 44),
                BorderStyle = BorderStyle.FixedSingle
            };

            btnCameraStart = CreateButton("▶ Kamera Başlat", MintColor, new Point(690, 15), 143);
            btnCameraStop = CreateButton("■ Kamera Durdur", RedColor, new Point(842, 15), 143);
            btnSelectImage = CreateButton("Görüntü Seç", LavenderColor, new Point(690, 57), 143);
            btnSaveFrame = CreateButton("Kareyi Kaydet", LightMintColor, new Point(842, 57), 143);

            Label lblDataTitle = new Label
            {
                Text = "Örnek ekleme ve eğitim",
                Location = new Point(690, 105),
                AutoSize = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = PinkColor
            };
            Label lblLabel = new Label { Text = "Etiket (isim):", Location = new Point(690, 132), AutoSize = true };
            txtLabel = new TextBox { Location = new Point(690, 152), Width = 295, MaxLength = 30 };

            btnAddSample = CreateButton("+ Örnek Ekle", PinkColor, new Point(690, 185), 143);
            btnTrain = CreateButton("Modeli Eğit", MintColor, new Point(842, 185), 143);

            lstLabels = new ListBox
            {
                Location = new Point(690, 228),
                Size = new Size(295, 120),
                BorderStyle = BorderStyle.FixedSingle,
                ForeColor = TextColor
            };
            btnOpenFolder = CreateButton("Örnek Klasörünü Aç", GrayColor, new Point(690, 355), 295);

            Label lblResultTitle = new Label
            {
                Text = "Tanıma sonucu",
                Location = new Point(690, 400),
                AutoSize = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = MintColor
            };
            lblResult = new Label
            {
                Location = new Point(690, 425),
                Size = new Size(295, 85),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(4),
                ForeColor = TextColor,
                Text = "Henüz görüntü yok."
            };

            lblStatus = new Label
            {
                Location = new Point(15, 525),
                Size = new Size(970, 25),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = TextColor,
                Text = "Durum: Başlatılıyor..."
            };

            Controls.AddRange(new Control[]
            {
                picPreview, btnCameraStart, btnCameraStop, btnSelectImage, btnSaveFrame,
                lblDataTitle, lblLabel, txtLabel, btnAddSample, btnTrain, lstLabels, btnOpenFolder,
                lblResultTitle, lblResult, lblStatus
            });

            // Model yüklenene kadar butonlar pasif
            foreach (Button button in new[] { btnCameraStart, btnCameraStop, btnSelectImage, btnSaveFrame, btnAddSample, btnTrain })
                button.Enabled = false;
        }

        // Düz, renkli bir buton oluşturur; pasifken otomatik griye döner
        private static Button CreateButton(string text, Color color, Point location, int width)
        {
            Button button = new Button
            {
                Text = text,
                Location = location,
                Size = new Size(width, 36),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = color,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderSize = 0;
            button.EnabledChanged += (s, e) => button.BackColor = button.Enabled ? color : DisabledColor;

            return button;
        }

        #endregion
    }
}
