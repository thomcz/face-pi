﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AForge.Video.DirectShow;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using Microsoft.Azure.NotificationHubs;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace FaceApiClientWindows.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        public RelayCommand TriggerButtonClick { get; set; }

        private BitmapImage _videoStream;
        public BitmapImage VideoStream
        {
            get {return _videoStream;}
            set
            {
                _videoStream = value;
                RaisePropertyChanged("VideoStream");
            }
        }
        private BitmapImage _faceImage;

        public BitmapImage FaceImage
        {
            get { return _faceImage; }
            set
            {
                _faceImage = value;
                RaisePropertyChanged("FaceImage");
            }
        }

        private string _identifyResultText;
        public string IdentifyResultText
        {
            get { return _identifyResultText; }
            set
            {
                _identifyResultText = value;
                RaisePropertyChanged("IdentifyResultText");
            }
        }

        public NotificationHubClient Hub
        {
            get
            {
                return NotificationHubClient.CreateClientFromConnectionString("Endpoint=sb://facenotificationhub.servicebus.windows.net/;SharedAccessKeyName=DefaultFullSharedAccessSignature;SharedAccessKey=MMUYZGD2MAPzEtZbpswbs0pvVsCuJiDwBAwOZ5aNpjg=", "FaceNotificationHub");
            }
        }

        public FaceServiceClient FaceServiceClient
        {
            get
            {
                return new FaceServiceClient("7865553ef2f14849b7e092c82605c00e");
            }
        }

        private Bitmap _lastFrame;

        public MainViewModel()
        {
            TriggerButtonClick = new RelayCommand(CheckFace);

            //Initialize Camera
            FilterInfoCollection filter = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            var device = new VideoCaptureDevice(filter[0].MonikerString);
            device.NewFrame += Device_NewFrame;
            device.Start();
        }

        public async void CheckFace()
        {
            if (_lastFrame == null)
                return;

            Bitmap facebtm = (Bitmap)_lastFrame.Clone();
            _lastFrame.Dispose();

            Random rand = new Random();
            string extraNum = rand.Next(100000).ToString();
            string imageName = "yeah" + extraNum + ".jpg";


            facebtm.Save(imageName, ImageFormat.Jpeg);
            Face face;

            using (var fileStream = File.OpenRead(imageName))
            {
                Face[] results = await FaceServiceClient.DetectAsync(fileStream);

                if (results.Length == 0)
                {
                    MessageBox.Show("No Person found: Sending Message (but shoudn't)");
                    //SendNotification(facebtm);
                    return;
                }

                face = results[0];

                BitmapImage newImage = new BitmapImage(new Uri(@"C:\Users\Julian\Documents\Visual Studio 2015\Projects\FaceApiClientWindows\FaceApiClientWindows\bin\Debug\" + imageName));
                newImage.Freeze();
                Dispatcher.CurrentDispatcher.Invoke(() => FaceImage = newImage);
            }

            IdentifyResult[] identifyResult = await FaceServiceClient.IdentifyAsync("allowed-persons", new[] { face.FaceId }, 0.8f);

            if (identifyResult[0].Candidates.Length == 0)
            {
                MessageBox.Show("No Person Identified: Sending Message");
                Rectangle faceRectangel = new Rectangle(face.FaceRectangle.Left, face.FaceRectangle.Top, face.FaceRectangle.Width, face.FaceRectangle.Height);
                SendNotification(facebtm.Clone(faceRectangel, facebtm.PixelFormat));
                return;
            }

            IdentifyResultText = "";
            foreach (Candidate candidate in identifyResult[0].Candidates)
            {
                IdentifyResultText += "ID: " + candidate.PersonId + "  Confidence: " + candidate.Confidence + "\n";
            }
        }


        private void Device_NewFrame(object sender, AForge.Video.NewFrameEventArgs eventArgs)
        {
            _lastFrame = (Bitmap)eventArgs.Frame.Clone();

            MemoryStream ms = new MemoryStream();
            _lastFrame.Save(ms, ImageFormat.Bmp);
            ms.Seek(0, SeekOrigin.Begin);
            BitmapImage bi = new BitmapImage();
            bi.BeginInit();
            bi.StreamSource = ms;
            bi.EndInit();

            bi.Freeze();
            Dispatcher.CurrentDispatcher.Invoke(new ThreadStart(delegate
            {
               VideoStream = bi;
            }));
        }

        private async void SendNotification(Bitmap image)
        {
            PixelFormat pxFormat = image.PixelFormat;
            Bitmap thumbnail = (Bitmap)GetThumbnail(image);
            thumbnail.Save("thumb.png");

            var toast3 = "{ \"data\": {\"message\":\"Who's that???\", \"imagePixelFormat\":\"" + pxFormat + "\", \"image\":\"" + CreateBase64Image(thumbnail) + "\" }}";

            Encoding ascii = Encoding.ASCII;
            int bytes = ascii.GetByteCount(toast3);
            await Hub.SendGcmNativeNotificationAsync(toast3);
        }

        private string CreateBase64Image(Bitmap image)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                /* Convert this image back to a base64 string */
                image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                return Convert.ToBase64String(ms.ToArray());
            }
        }

        private Image GetThumbnail(Bitmap image)
        {
            return image.GetThumbnailImage(25, 25, () => false, IntPtr.Zero);
        }
    }
}
