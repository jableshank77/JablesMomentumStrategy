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
	public class NQStrategy : Strategy
	{
		private int		scalpQuantity		= 1;		// Default setting for scalp contracts per trade
		private int		runnerQuantity		= 1;		// Default setting for runner contracts per trade
		private int		ProfitTargetTicks1	= 20;		// Default setting for how many Ticks away from AvgPrice is scalp target
		private int		ProfitTargetTicks2	= 40;		// Default setting for how many Ticks away from AvgPrice is runner target
		private int		breakEvenTicks		= 20;		// Default setting for ticks needed to acheive before stop moves to breakeven		
		private int		plusBreakEven		= 2; 		// Default setting for amount of ticks past breakeven to actually breakeven
		private int		trailProfitTrigger	= 20;		// Default Setting for trail trigger ie the number of ticks movede after break even befor activating TrailStep
		private int		trailStepTicks		= 8;		// Default setting for number of ticks advanced in the trails - take into consideration the barsize as is calculated/advanced next bar
		private int 	BarTraded 			= 0; 		// Default setting for Bar number that trade occurs	
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
				Name								= "NQStrategy";
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
				EntriesPerDirection = 1;
    			EntryHandling = EntryHandling.UniqueEntries;


			}
			else if (State == State.Configure)
			{
				SetProfitTarget(@"Scalp Entry", CalculationMode.Ticks, ProfitTargetTicks1);	
				SetProfitTarget(@"Runner Entry", CalculationMode.Ticks, ProfitTargetTicks2);	
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
						SetStopLoss(CalculationMode.Price, Low[2]);
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
						SetStopLoss(CalculationMode.Price, High[2]);
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
           	if (TimeCheck && Flat && IsFirstTickOfBar && Bullish)
            {	
				FillLongEntry1();
            }

		    // ShortEntry
            if (TimeCheck && Flat && IsFirstTickOfBar && Bearish)
            {	
				FillShortEntry1();
            }	
		}
		
		private void FillLongEntry1()
		{
			EnterLong(Convert.ToInt32(scalpQuantity), @"Scalp Entry");
			EnterLong(Convert.ToInt32(runnerQuantity), @"Runner Entry");
		}
			
		private void FillShortEntry1()
		{
			EnterShort(Convert.ToInt32(scalpQuantity), @"Scalp Entry");
			EnterShort(Convert.ToInt32(runnerQuantity), @"Runner Entry");
		}				

	}
}
