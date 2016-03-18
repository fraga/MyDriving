﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using MyDriving.Helpers;
using MyDriving.Utils;
using MvvmHelpers;
using MyDriving.DataObjects;
using System.Collections.ObjectModel;
using Plugin.DeviceInfo;

namespace MyDriving.ViewModel
{
	public class PastTripsDetailViewModel : ViewModelBase
	{
		public Trip Trip { get; set; }

        public PastTripsDetailViewModel()
        {
            FuelConsumptionUnits = Settings.MetricUnits ? "Liters" : "Gallons";
            DistanceUnits = Settings.MetricDistance ? "Kilometers" : "Miles";
        }

        public PastTripsDetailViewModel(Trip trip) : this()
		{
			Title = trip.Name;
            //make sure the points are ordered
            trip.Points = trip.Points.OrderBy(p => p.Sequence).ToArray();
			Trip = trip;
		}

        TripPoint position;
        public TripPoint CurrentPosition
        {
            get { return position; }
            set
			{
				SetProperty(ref position, value);
				UpdateTripInformationForPoint();
			}
		}

		string elapsedTime = "0s";
        public string ElapsedTime
        {
            get { return elapsedTime; }
            set { SetProperty(ref elapsedTime, value); }
        }

        string distance = "0.0";
        public string Distance
        {
            get { return distance; }
            set { SetProperty(ref distance, value); }
        }

        string distanceUnits = "miles";
        public string DistanceUnits
        {
            get { return distanceUnits; }
            set { SetProperty(ref distanceUnits, value); }
        }

        string fuelConsumption = "N/A";
        public string FuelConsumption
        {
            get { return fuelConsumption; }
            set { SetProperty(ref fuelConsumption, value); }
        }

        string fuelConsumptionUnits = "gal";
        public string FuelConsumptionUnits
        {
            get { return fuelConsumptionUnits; }
            set { SetProperty(ref fuelConsumptionUnits, value); }
        }

		string speed = "0.0";
		public string Speed
		{
			get { return speed; }
			set { SetProperty(ref speed, value); }
		}

		string speedUnits = "Mph";
		public string SpeedUnits
		{
			get { return speedUnits; }
			set { SetProperty(ref speedUnits, value); }
		}

        ICommand  loadTripCommand;
        public ICommand LoadTripCommand =>
        loadTripCommand ?? (loadTripCommand = new RelayCommand<string>(async (id) => await ExecuteLoadTripCommandAsync(id))); 

        public async Task ExecuteLoadTripCommandAsync(string id)
        {
            if(IsBusy)
                return;

            var progress = Acr.UserDialogs.UserDialogs.Instance.Loading("Loading trip details...", maskType: Acr.UserDialogs.MaskType.Clear);
            
            try 
            {
                IsBusy = true;

                Trip = await StoreManager.TripStore.GetItemAsync(id);
                Title = Trip.Name;
            }
            catch (Exception ex) 
            {
                Logger.Instance.Report(ex);
            } 
            finally 
            {
                progress?.Dispose();
                IsBusy = false;
            }
        }

		public void UpdateTripInformationForPoint()
		{
			var timeDif = position.RecordedTimeStamp - Trip.RecordedTimeStamp;

			//track seconds, minutes, then hours
			if (timeDif.TotalMinutes < 1)
				ElapsedTime = $"{timeDif.Seconds}s";
			else if (timeDif.TotalHours < 1)
				ElapsedTime = $"{timeDif.Minutes}m";
			else
				ElapsedTime = $"{(int)timeDif.TotalHours}h {timeDif.Minutes}m";

			var previousPoints = Trip.Points.Where(p => p.RecordedTimeStamp <= position.RecordedTimeStamp).ToArray();
            var obdPoints = previousPoints.Where(p => p.HasOBDData && p.MassFlowRate > -1).ToArray();

			var totalConsumptionPoints = obdPoints.Length;
			var totalConsumption = obdPoints.Sum(s => s.MassFlowRate);

			if (totalConsumptionPoints > 0)
			{
				var fuelUsedLiters = (totalConsumption / totalConsumptionPoints) * timeDif.TotalHours * 0.3047247;
				FuelConsumption = Settings.MetricUnits ? fuelUsedLiters.ToString("N2") : (fuelUsedLiters * .264172).ToString("N2");
			}
			else
			{
				FuelConsumption = "N/A";
			}

			FuelConsumptionUnits = Settings.MetricUnits ? "Liters" : "Gallons";
			DistanceUnits = Settings.MetricDistance ? "Kilometers" : "Miles";
			Speed = position.Speed.ToString();
			SpeedUnits = Settings.MetricDistance ? "Kmh" : "Mph";

			if (previousPoints.Length > 2)
			{
				double totalDistance = 0;
				var latPrevious = previousPoints[0].Latitude;
				var longPrevious = previousPoints[0].Longitude;
				for (int i = 1; i < previousPoints.Length; i++)
				{
					var current = previousPoints[i];

					totalDistance += DistanceUtils.CalculateDistance(current.Latitude, current.Longitude, latPrevious, longPrevious);

					latPrevious = current.Latitude;
					longPrevious = current.Longitude;
				}

				Distance = (Settings.Current.MetricDistance ? (totalDistance * 1.60934) : totalDistance).ToString("f");
			}
			else
			{
				Distance = "0.0";
			}

			OnPropertyChanged("Stats");
		}
	}
}