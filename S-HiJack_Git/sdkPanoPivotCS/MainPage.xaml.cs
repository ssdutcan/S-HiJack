/* 
    Copyright (c) 2011 Microsoft Corporation.  All rights reserved.
    Use of this sample source code is subject to the terms of the Microsoft license 
    agreement under which you licensed this sample source code and is provided AS-IS.
    If you did not accept the terms of the license agreement, you are not authorized 
    to use this sample source code.  For the terms of the license, please see the 
    license agreement between you and Microsoft.
  
    To see all Code Samples for Windows Phone, visit http://go.microsoft.com/fwlink/?LinkID=219604 
  
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Phone.Controls;
using Visifire.Charts;
using Visifire.Commons;
using System.Windows.Threading;
using Microsoft.Xna.Framework;
using HiJack;
using Microsoft.Devices;
using System.Device.Location;
using System.Collections.ObjectModel;
using Microsoft.Phone.Controls.Maps.Platform;
using Microsoft.Phone.Controls.Maps;

namespace sdkPanoPivotCS
{
    public class XNAFrameworkDispatcherService : IApplicationService
    {
        private DispatcherTimer frameworkDispatcherTimer;
        public XNAFrameworkDispatcherService()
        {
            this.frameworkDispatcherTimer = new DispatcherTimer();
            this.frameworkDispatcherTimer.Interval = TimeSpan.FromTicks(333333);
            this.frameworkDispatcherTimer.Tick += frameworkDispatcherTimer_Tick;
            FrameworkDispatcher.Update();
        }

        void frameworkDispatcherTimer_Tick(object sender, EventArgs e) { FrameworkDispatcher.Update(); }

        void IApplicationService.StartService(ApplicationServiceContext context) { this.frameworkDispatcherTimer.Start(); }

        void IApplicationService.StopService() { this.frameworkDispatcherTimer.Stop(); }
    }

    public partial class PivotPage1 : PhoneApplicationPage
    {
        private HijackX hijack = new HijackX(44100);
        MediaElement media = new MediaElement();
        private int nodeId = 1;
        private int previous = 0;
        private int count = 0;
        private bool hasNode1 = false;
        private bool hasNode2 = false;
        private bool hasNode3 = false;
        private Pushpin pin1 = new Pushpin();
        private Pushpin pin2 = new Pushpin();
        private Pushpin pin3 = new Pushpin();



        Array array = Array.CreateInstance(typeof(Int32), 30);


        public PivotPage1()
        {
            InitializeComponent();
            media.SetSource(hijack);
            hijack.DataReady += new DataReadyEventHandler(hijack_DataReady);
            media.Play();
            CreateChart();
            CreatPushpin();
           
        }

        void hijack_DataReady(object sender, DataReadyEventArgs e)
        {
            //data是节点id
            if (int.Parse(e.ReceiveData.ToString()) > 128)
            {
                previous = int.Parse(e.ReceiveData.ToString());
            }
            //data是温度值
            else 
            {
                if (previous == nodeId + 128)
                {
                    for (Int32 i = 0; i < 4; i++)
                    {
                        // Update DataPoint YValue propert
                        chart.Series[0].DataPoints[i].YValue = chart.Series[0].DataPoints[i + 1].YValue; // Changing the dataPoint YValue at runtime
                    }
                    chart.Series[0].DataPoints[4].YValue = double.Parse(e.ReceiveData.ToString()) + rand.NextDouble();
                }
            }


            count = (count + 1) % 20;
            array.SetValue(int.Parse(e.ReceiveData.ToString()), count);
            if (count == 19)
            {
                hasNode1 = false;
                hasNode2 = false;
                hasNode3 = false;
                for (int i = 0; i < 20; i++)
                { 
                    if(array.GetValue(i).Equals(129))
                    {
                        hasNode1 = true;
                        pin1.Background = new SolidColorBrush(Colors.Red);
                    }
                    else if (array.GetValue(i).Equals(130))
                    {
                        hasNode2 = true;
                        pin2.Background = new SolidColorBrush(Colors.Red);
                    }
                    else if (array.GetValue(i).Equals(131))
                    {
                        hasNode3 = true;
                        pin3.Background = new SolidColorBrush(Colors.Red);
                    }
                }

                if (hasNode1 == false)
                {
                    pin1.Background = new SolidColorBrush(Colors.Gray);
                }
                if (hasNode2 == false)
                {
                    pin2.Background = new SolidColorBrush(Colors.Gray);
                }
                if (hasNode3 == false)
                {
                    pin3.Background = new SolidColorBrush(Colors.Gray);
                }

                for (int i = 0; i < 20; i++)
                {
                    array.SetValue(i, 0);
                }
                    
            }
            //for (Int32 i = 0; i < 4; i++)
            //{
            //    // Update DataPoint YValue propert
            //    chart.Series[0].DataPoints[i].YValue = chart.Series[0].DataPoints[i + 1].YValue; // Changing the dataPoint YValue at runtime
            //}

            //chart.Series[0].DataPoints[4].YValue = int.Parse(e.ReceiveData.ToString());
            //if (int.Parse(e.ReceiveData.ToString())>100)
            //{
            //    //VibrateController vc = VibrateController.Default;
            //    //vc.Start(TimeSpan.FromMilliseconds(100));
            //}
                
        }



        Chart chart;                                            // Visifire chart
        Random rand = new Random(DateTime.Now.Millisecond);     // Create a random class variable
        System.Windows.Threading.DispatcherTimer timer = new    // Create a timer object
            System.Windows.Threading.DispatcherTimer();

        public void CreateChart()
        {
            // Create a new instance of a Chart
            chart = new Chart();

            // Create a new instance of Title
            Title title = new Title();

            // Set title property
            title.Text = "CO2 Temperature";

            // Add title to Titles collection
            chart.Titles.Add(title);

            // Create a new instance of DataSeries
            DataSeries dataSeries = new DataSeries();

            // Set DataSeries property
            dataSeries.RenderAs = RenderAs.Spline;

            // Create a DataPoint
            DataPoint dataPoint;

            for (int i = 0; i < 5; i++)
            {
                // Create new instance of DataPoint
                dataPoint = new DataPoint();

                // Set YValue for a DataPoint
                dataPoint.YValue = 0;

                // Add dataPoint to DataPoints collection.
                dataSeries.DataPoints.Add(dataPoint);
            }

            // Add dataSeries to Series collection.
            chart.Series.Add(dataSeries);

            // Attach a Loaded event to chart in order to attach a timer's Tick event
            chart.Loaded += new RoutedEventHandler(chart_Loaded);

            // Add chart to Chart Grid
            ContentPanel.Children.Add(chart);
        }

        void chart_Loaded(object sender, RoutedEventArgs e)
        {
            //timer.Tick += new EventHandler(timer_Tick);
            //timer.Interval = new TimeSpan(0, 0, 0, 0, 1500);
        }

        void timer_Tick(object sender, EventArgs e)
        {
            for (Int32 i = 0; i < 4; i++)
            {
                // Update DataPoint YValue property
                chart.Series[0].DataPoints[i].YValue = chart.Series[0].DataPoints[i+1].YValue; // Changing the dataPoint YValue at runtime
            }
        }

        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            // timer starts
            timer.Start();
        }

        private void UpdateStopButton_Click(object sender, RoutedEventArgs e)
        {
            // timer stops
            timer.Stop();
        }

        //-----------------------------------------------------------------

        public void CreatPushpin()
        {
            double zoomLevel = 17;
            Location location1 = new Location();
            location1.Latitude = 39.08246430468703;
            location1.Longitude = 121.80811822414401;
            map1.Mode = new AerialMode(true);
            map1.SetView(location1, zoomLevel);

            Pushpin pin = new Pushpin();
            pin.Location = new GeoCoordinate(39.084071618397154, 121.80735647678375);
            pin.Content = "0";
            pin.Style = (Style)(Application.Current.Resources["PushpinStyle"]);
            pin.Background = new SolidColorBrush(Colors.Green);
            //pin.Tap += new EventHandler<GestureEventArgs>(pin_Tap);
            map1.Children.Add(pin);


            //pin = new Pushpin();
            pin1.Location = new GeoCoordinate(39.08246430468703, 121.80811822414401);
            pin1.Content = "1";
            pin1.Style = (Style)(Application.Current.Resources["PushpinStyle"]);
            pin1.Background = new SolidColorBrush(Colors.Gray);
            pin1.Tap +=new EventHandler<GestureEventArgs>(pin_Tap);
            map1.Children.Add(pin1);

            //pin = new Pushpin();
            pin2.Location = new GeoCoordinate(39.081773065250125, 121.80607974529269);
            pin2.Content = "2";
            pin2.Style = (Style)(Application.Current.Resources["PushpinStyle"]);
            pin2.Background = new SolidColorBrush(Colors.Gray);
            pin2.Tap +=new EventHandler<GestureEventArgs>(pin_Tap);
            map1.Children.Add(pin2);

            //pin = new Pushpin();
            pin3.Location = new GeoCoordinate(39.081773065250125, 121.80683076381686);
            pin3.Content = "3";
            pin3.Style = (Style)(Application.Current.Resources["PushpinStyle"]);
            pin3.Background = new SolidColorBrush(Colors.Gray);
            pin3.Tap +=new EventHandler<GestureEventArgs>(pin_Tap);
            map1.Children.Add(pin3);

        }

        void pin_Tap(object sender, GestureEventArgs e)
        {
            var pushPin = sender as Pushpin;
            nodeId = int.Parse(pushPin.Content.ToString());
            MyPivot.SelectedItem = PivotItem1;
        }
            
        
    }
 
}
