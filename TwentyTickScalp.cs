#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
	public class TwentyTickScalp : Strategy
	{
		private int		scalpQuantity		= 1;		// Default setting for scalp contracts per trade
		private int		runnerQuantity		= 1;		// Default setting for runner contracts per trade
		private int		breakEvenTicks		= 10;		// Default setting for ticks needed to acheive before stop moves to breakeven		
		private int		plusBreakEven		= 2; 		// Default setting for amount of ticks past breakeven to actually breakeven
		private int		profitTargetTicks	= 20;		// Default setting for how many Ticks away from AvgPrice is profit target
        // private int		stopLossTicks		= 6;		// Default setting for stoploss. Ticks away from AvgPrice		
		private int		trailProfitTrigger	= 20;		// 8 Default Setting for trail trigger ie the number of ticks movede after break even befor activating TrailStep
		private int		trailStepTicks		= 8;		// 2 Default setting for number of ticks advanced in the trails - take into consideration the barsize as is calculated/advanced next bar
		private int 	BarTraded 			= 0; 		// Default setting for Bar number that trade occurs	
		
		private bool	showLines			= false;		// Turn on/off the profit targett, stoploss and trailing stop plots  // new for NT8
		
		private double	initialBreakEven	= 0; 		// Default setting for where you set the breakeven
		private double 	previousPrice		= 0;		// previous price used to calculate trailing stop
		private double 	newPrice			= 0;		// Default setting for new price used to calculate trailing stop
		private double	stopPlot			= 0;		// Value used to plot the stop level
		

		// 7/8/2020 - Changed from Calculate.OnBarClose to Calculate.OnPriceChange for correct stop placement
		// 7/8/2020 - Relocated entry logic to occur after Market position sequencing for "Best Practices"
		
		
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description							= @"The Name is descriptive enough.";
				Name								= "TwentyTickScalp";
				Calculate							= Calculate.OnPriceChange;
				EntriesPerDirection					= 1;
				EntryHandling						= EntryHandling.AllEntries;
				IsExitOnSessionCloseStrategy		= false;
				ExitOnSessionCloseSeconds			= 30;
				IsFillLimitOnTouch					= false;
				MaximumBarsLookBack					= MaximumBarsLookBack.TwoHundredFiftySix;
				OrderFillResolution					= OrderFillResolution.Standard;
				Slippage							= 0;
				StartBehavior						= StartBehavior.WaitUntilFlat;
				TimeInForce							= TimeInForce.Gtc;
				TraceOrders							= false;
				RealtimeErrorHandling				= RealtimeErrorHandling.StopCancelClose;
				StopTargetHandling					= StopTargetHandling.PerEntryExecution;
				BarsRequiredToTrade					= 20;

				AddPlot(new Stroke(Brushes.Lime, 2), PlotStyle.Hash, "ProfitTarget");
				AddPlot(new Stroke(Brushes.Red, 2), PlotStyle.Line, "StopLoss");

			}
			else if (State == State.Configure)
			{
				SetProfitTarget(CalculationMode.Ticks, profitTargetTicks);	
			}
		}
		

		protected override void OnBarUpdate()
		{
			if (CurrentBar < BarsRequiredToTrade) return;	

			// keep the below code intact for use with a fixed stop, a break even stop and a profit trailing stop =================			
			switch (Position.MarketPosition)
            {
				// Resets the stop loss to the original value when all positions are closed
                case MarketPosition.Flat:
					previousPrice = 0;
                    break;
				
					   
                case MarketPosition.Long:
						
					if (previousPrice == 0)
					{
						SetStopLoss(CalculationMode.Price, Low[1]);
					}
					
                    // Once the price is greater than entry price + breakEvenTicks ticks, set stop loss to plusBreakeven ticks
                    if (Close[0] > Position.AveragePrice + breakEvenTicks * TickSize  && previousPrice == 0)
                    {
						initialBreakEven = Position.AveragePrice + plusBreakEven * TickSize;
                        SetStopLoss(CalculationMode.Price, initialBreakEven);
						previousPrice = Position.AveragePrice;
                    }
					// Once at breakeven wait till trailProfitTrigger is reached before advancing stoploss by trailStepTicks size step
					else if (previousPrice	!= 0 ////StopLoss is at breakeven
 							&& GetCurrentAsk() > previousPrice + trailProfitTrigger * TickSize )
					{
						newPrice = previousPrice + trailStepTicks * TickSize; 	// Calculate trail stop adjustment
						SetStopLoss(CalculationMode.Price, newPrice);			// Readjust stoploss level		
						previousPrice = newPrice;				 				// save for price adjust on next candle
					}
                    break;
					
					
                case MarketPosition.Short:
					
					if (previousPrice == 0) 
					{
						SetStopLoss(CalculationMode.Price, High[1]);
					}
					
                    // Once the price is Less than entry price - breakEvenTicks ticks, set stop loss to breakeven
                    if (Close[0] < Position.AveragePrice - breakEvenTicks * TickSize && previousPrice == 0)
                    {
						initialBreakEven = Position.AveragePrice - plusBreakEven * TickSize;
                        SetStopLoss(CalculationMode.Price, initialBreakEven);
						previousPrice = Position.AveragePrice;
                    }
					// Once at breakeven wait till trailProfitTrigger is reached before advancing stoploss by trailStepTicks size step
					else if (previousPrice	!= 0 ////StopLoss is at breakeven
 							&& GetCurrentAsk() < previousPrice - trailProfitTrigger * TickSize )
					{
						newPrice = previousPrice - trailStepTicks * TickSize;
						SetStopLoss(CalculationMode.Price, newPrice);
						previousPrice = newPrice;
					}				

                    break;
                default:
                    break;
			}	
			
			// Begin the Entry Logic section *********

			bool TimeCheck = (Times[0][0].TimeOfDay > new TimeSpan(07, 0, 0))
			 	&& (Times[0][0].TimeOfDay < new TimeSpan(11, 45, 0));
			bool Flat = (Position.MarketPosition == MarketPosition.Flat);
			bool Long = (Position.MarketPosition == MarketPosition.Long);
			bool Short = (Position.MarketPosition == MarketPosition.Short);
			// Current bar closed higher than prior bar close & current bar closed above its open
			bool Bullish = Close[1] > Close[2] && Close[1] > Open[1] && Close[2] > Open[2]; 
			// Current bar closed lower than prior bar close & current bar closed below its open
			bool Bearish = Close[1] < Close[2] && Close[1] < Open[1] && Close[2] < Open[2]; 
			
			// LongEntry
           	if (TimeCheck && IsFirstTickOfBar && Bullish)
            {	
				FillLongEntry1();
            }

		    // ShortEntry
            if (TimeCheck && IsFirstTickOfBar && Bearish)
            {	
				FillShortEntry1();
            }	
		}
		
		private void FillLongEntry1()
		{
			EnterLong(Convert.ToInt32(scalpQuantity), @"Scalp Entry");
			BarTraded = CurrentBar;  // save the current bar so only one entry per bar
		}
			
		private void FillShortEntry1()
		{
			EnterShort(Convert.ToInt32(scalpQuantity), @"Scalp Entry");
			BarTraded = CurrentBar;  // save the current bar so only one entry per bar
		}				

		
		#region Properties
		[Range(0, int.MaxValue)]
		[NinjaScriptProperty]
		[Display(Name="Profit Target Ticks", Description="Number of ticks away from entry price for the Profit Target order", Order=2, GroupName="Parameters")]
		public int ProfitTargetTicks
		{
			get { return profitTargetTicks; }
			set { profitTargetTicks = value; }
		}

		[Range(0, int.MaxValue)]
		[NinjaScriptProperty]
		[Display(Name="BreakEven Ticks Trigger", Description="Number of ticks in Profit to trigger stop to move to Plus Breakeven ticks level", Order=3, GroupName="Parameters")]
		public int BreakEvenTicks
		{
			get {return breakEvenTicks;}
			set {breakEvenTicks = value;}
		}

		[Range(0, int.MaxValue)]
		[NinjaScriptProperty]
		[Display(Name="BreakEven Ticks level", Description="Number of ticks past breakeven for breakeven stop (can be zero)", Order=4, GroupName="Parameters")]
		public int PlusBreakEven
		{
			get { return plusBreakEven; }
			set { plusBreakEven = value; }
		}

		[Range(0, int.MaxValue)]
		[NinjaScriptProperty]
		[Display(Name="Trail Profit Trigger", Description="Number of ticks in profit to trigger trail stop action", Order=5, GroupName="Parameters")]
		public int TrailProfitTrigger
		{
			get {return trailProfitTrigger;}
			set {trailProfitTrigger = value;}
		}
		
		[Range(0, int.MaxValue)]
		[NinjaScriptProperty]
		[Display(Name="Trail Step Ticks", Description="Number of ticks to step for each adjustment of trail stop", Order=6, GroupName="Parameters")]
		public int TrailStepTicks
		{
			get {return trailStepTicks;}
			set {trailStepTicks = value;}
		}		

		[Browsable(false)]
		[XmlIgnore]
		public Series<double> ProfitTarget
		{
			get { return Values[0]; }
		}

		[Browsable(false)]
		[XmlIgnore]
		public Series<double> StopLoss
		{
			get { return Values[1]; }
		}


		#endregion

	}
}