﻿using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Device.Location;

namespace sdkPanoPivotCS
{
    public class PushpinModel
    {
        public GeoCoordinate Location { get; set; }
        public Uri Icon { get; set; }
        public string TypeName { get; set; }
        public PushpinModel Clone(GeoCoordinate location)
        {
            return new PushpinModel
            {
                Location = location,
                TypeName = TypeName,
                Icon = Icon
            };
        }
    }
}
