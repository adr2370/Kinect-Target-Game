/////////////////////////////////////////////////////////////////////////
//
// This module contains code to do a basic green screen.
//
// Copyright © Microsoft Corporation.  All rights reserved.  
// This code is licensed under the terms of the 
// Microsoft Kinect for Windows SDK (Beta) from Microsoft Research 
// License Agreement: http://research.microsoft.com/KinectSDK-ToU
//
/////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Text;
using System.Media;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Net.Sockets;
using Microsoft.Research.Kinect.Nui;
using Coding4Fun.Kinect.Wpf;
using ShapeGame_Speech;
using ShapeGame_Utils;

namespace SkeletalTracking
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        bool tracking = true;
        Recognizer recognizer = null;
        float rightHandX = -1;
        float rightHandY = -1;
        float leftHandX = -1;
        float leftHandY = -1;
        float targetX = -1;
        float targetY = -1;
        Random rand = new Random();
        public MainWindow()
        {
            InitializeComponent();
            target.Visibility = Visibility.Hidden;
        }

        //Kinect Runtime
        Runtime nui = new Runtime();

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            #region VideoStuff
            nui.VideoFrameReady += new EventHandler<ImageFrameReadyEventArgs>(nui_VideoFrameReady);

            //Initialize to do skeletal tracking
            nui.Initialize(RuntimeOptions.UseSkeletalTracking | RuntimeOptions.UseColor);

            nui.VideoStream.Open(ImageStreamType.Video, 2, ImageResolution.Resolution640x480, ImageType.Color);

            #region TransformSmooth
            //Must set to true and set after call to Initialize
            nui.SkeletonEngine.TransformSmooth = true;

            //Use to transform and reduce jitter
            var parameters = new TransformSmoothParameters
            {
                Smoothing = 0.75f,
                Correction = 0.0f,
                Prediction = 0.0f,
                JitterRadius = 0.05f,
                MaxDeviationRadius = 0.04f
            };

            nui.SkeletonEngine.SmoothParameters = parameters; 

            #endregion

            //add event to receive skeleton data
            nui.SkeletonFrameReady += new EventHandler<SkeletonFrameReadyEventArgs>(nui_SkeletonFrameReady);

            #endregion

            try
            {
                recognizer = new Recognizer();
            }
            catch
            {
                recognizer = null;
            }
            if ((recognizer == null) || !recognizer.IsValid())
            {
                recognizer = null;
            }
            else
                recognizer.SaidSomething += recognizer_SaidSomething;

            //send data
            string[] stuff = new string[(int)JointID.Count * 2];
            targetX = (float)(rand.Next(540) + 50);
            targetY = (float)(rand.Next(230) + 100);
            Canvas.SetLeft(target, targetX-100);
            Canvas.SetTop(target, targetY-100);
            Console.WriteLine(targetX + " " + targetY);
        }

        void recognizer_SaidSomething(object sender, Recognizer.SaidSomethingArgs e)
        {
            switch (e.Verb)
            {
                case Recognizer.Verbs.Stop:
                    target.Visibility = Visibility.Hidden;
                    break;
                case Recognizer.Verbs.Start:
                    target.Visibility = Visibility.Visible;
                    ScoreNumber.Content = "0";
                    break;
            }
        }

        void nui_VideoFrameReady(object sender, ImageFrameReadyEventArgs e)
        {
            //Manually create BitmapSource for Video
            PlanarImage imageData = e.ImageFrame.Image;
            image1.Source = BitmapSource.Create(imageData.Width, imageData.Height, 96, 96, PixelFormats.Bgr32, null, imageData.Bits, imageData.Width * imageData.BytesPerPixel);
        }

        void nui_SkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            
            SkeletonFrame allSkeletons = e.SkeletonFrame;

            //get the first tracked skeleton
            SkeletonData skeleton = (from s in allSkeletons.Skeletons
                                     where s.TrackingState == SkeletonTrackingState.Tracked
                                     select s).FirstOrDefault();


            #region Set Positions
            //set position
            SetEllipsePosition(RHand, skeleton.Joints[JointID.HandRight]);
            var scaledJoint = skeleton.Joints[JointID.HandRight].ScaleTo(640, 480, .5f, .5f);
            rightHandX = scaledJoint.Position.X;
            rightHandY = scaledJoint.Position.Y;
            SetEllipsePosition(LHand, skeleton.Joints[JointID.HandLeft]);
            scaledJoint = skeleton.Joints[JointID.HandLeft].ScaleTo(640, 480, .5f, .5f);
            leftHandX = scaledJoint.Position.X;
            leftHandY = scaledJoint.Position.Y;
            if (target.Visibility == Visibility.Visible)
            {
                if (Math.Sqrt((rightHandX - targetX) * (rightHandX - targetX) + (rightHandY - targetY) * (rightHandY - targetY)) < 50 ||
                Math.Sqrt((leftHandX - targetX) * (leftHandX - targetX) + (leftHandY - targetY) * (leftHandY - targetY)) < 50)
                {
                    int currScore = int.Parse((string)ScoreNumber.Content);
                    currScore++;
                    ScoreNumber.Content = currScore + "";
                    targetX = (float)(rand.Next(540) + 50);
                    targetY = (float)(rand.Next(230) + 100);
                    while (Math.Sqrt((rightHandX - targetX) * (rightHandX - targetX) + (rightHandY - targetY) * (rightHandY - targetY)) < 50 ||
                           Math.Sqrt((leftHandX - targetX) * (leftHandX - targetX) + (leftHandY - targetY) * (leftHandY - targetY)) < 50)
                    {
                        targetX = (float)(rand.Next(540) + 50);
                        targetY = (float)(rand.Next(230) + 100);
                    }
                    Canvas.SetLeft(target, targetX - 100);
                    Canvas.SetTop(target, targetY - 100);
                }
            }
            #endregion

            #region Check Inferred
            if (!tracking || skeleton.Joints[JointID.HandRight].TrackingState == JointTrackingState.NotTracked)
            {
                Canvas.SetLeft(RHand, -100);
                Canvas.SetTop(RHand, -100);
            }
            if (!tracking || skeleton.Joints[JointID.HandLeft].TrackingState == JointTrackingState.NotTracked)
            {
                Canvas.SetLeft(LHand, -100);
                Canvas.SetTop(LHand, -100);
            }
            #endregion

            //set position
            #region recordData
            string[] stuff = new string[(int)JointID.Count * 4];
            for (int i = 0; i < (int)JointID.Count; i++)
            {
                if (skeleton.Joints[(JointID)i].TrackingState == JointTrackingState.Tracked)
                {
                    stuff[4 * i] = skeleton.Joints[(JointID)i].ID.ToString();
                    stuff[4 * i + 1] = skeleton.Joints[(JointID)i].Position.X.ToString();
                    stuff[4 * i + 2] = skeleton.Joints[(JointID)i].Position.Y.ToString();
                    stuff[4 * i + 3] = skeleton.Joints[(JointID)i].Position.Z.ToString();
                }
                else
                {
                    stuff[4 * i] = skeleton.Joints[(JointID)i].ID.ToString();
                    stuff[4 * i + 1] = "Not Found X";
                    stuff[4 * i + 2] = "Not Found Y";
                    stuff[4 * i + 3] = "Not Found Z";
                }
            }
            try
            {
                System.IO.File.WriteAllLines(@"C:\Users\Alexander Ramirez\Desktop\Kinect E87\SkeletalTracking\Data.txt", stuff);
            }
            catch (Exception ex) { }
            #endregion
        }

        private void SetEllipsePosition(FrameworkElement ellipse, Joint joint)
        {
            var scaledJoint = joint.ScaleTo(640, 480, .5f, .5f);

            Canvas.SetLeft(ellipse, scaledJoint.Position.X);
            Canvas.SetTop(ellipse, scaledJoint.Position.Y);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            //Cleanup
            nui.Uninitialize();
        }

        private void target_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {

        }
    }
}
