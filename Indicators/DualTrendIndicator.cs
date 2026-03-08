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
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion


// This namespace holds Indicators in this folder and is required. Do not change it.
namespace NinjaTrader.NinjaScript.Indicators.PropTraderz
{
    /// <summary>
    /// An integrated indicator combining momentum analysis and trend detection to identify potential trading opportunities.
    /// Features customizable parameters optimized for both Renko and time-based charts, visual signals on the main chart,
    /// and dynamic subchart plots indicating overbought and oversold conditions.
	/// Modified by PropTraderz
	/// www.proptraderz.com
    /// For Educational Purposes Only.
    /// </summary>
    public class DualTrendIndicator : Indicator
    {
        #region Variables for BouncePoint
        private double cciVal = 0.0;
        private double atrVal = 0.0;
        private double upTrend = 0.0;
        private double downTrend = 0.0;
        private Series<double> lineColor;
        private Series<bool> flatSection;
        private Series<double> _direction;
        private Series<double> _signal;

        private bool ArrowPrintedUP = false;
        private bool ArrowPrintedDOWN = false;
        private SimpleFont textFontSymbolWing = new SimpleFont("Wingdings", 10);//, FontStyle.Bold);
        private SimpleFont textFont;
        private SimpleFont textFont1;
        private SimpleFont textFont2;
        private SimpleFont textFont3;
        private int markersize = 30;

        private List<double> trendLinePoints = new List<double>();
        #endregion

        #region Variables for TradeZone
        private MIN MinLowRenko;
        private MAX MaxHighRenko;
        private MIN MinLowTime;
        private MAX MaxHighTime;
        private Series<double> RelDiffRenko, DiffRenko, SMISeriesRenko;
        private Series<double> RelDiffTime, DiffTime, SMISeriesTime;
        private EMA EMA0Renko, EMA1Renko, AvgRelRenko, AvgDiffRenko, SMIEMARenko;
        private EMA EMA0Time, EMA1Time, AvgRelTime, AvgDiffTime, SMIEMATime;
        #endregion

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                #region SetDefaults for DualTrendIndicator
                Description = @"An integrated indicator combining momentum analysis and trend detection to identify potential trading opportunities. Features customizable parameters optimized for both Renko and time-based charts, visual signals on the main chart, and dynamic subchart plots indicating overbought and oversold conditions";
                Name = "DualTrendIndicator";
                Calculate = Calculate.OnBarClose;//OnPriceChange;//
                MaximumBarsLookBack = MaximumBarsLookBack.TwoHundredFiftySix;
                IsOverlay = false; // Set to false so that plots appear in subchart
                DrawOnPricePanel = true; // Allows drawing objects on the main chart
                IsSuspendedWhileInactive = true;
                #endregion

                #region SetDefaults for BouncePoint
                cciPeriod = 2;//30;
                atrPeriod = 2;//50;
                atrMult = 0;//0.55;

                DrawArrows = true;//false;//
                ArrowDisplacement = 30;
                ArrowUpColor = Brushes.AliceBlue;
                ArrowDownColor = Brushes.AliceBlue;

                DrawEarlyArrows = true;
                EarlyArrowUpColor = Brushes.DodgerBlue;
                EarlyArrowDownColor = Brushes.DodgerBlue;

                PlotColor = Brushes.Yellow;
                // Removed the Trend plot to avoid plotting in subchart
                // AddPlot(new Stroke(Brushes.Black, 4), PlotStyle.Dot, "Trend");
                #endregion

                #region SetDefaults for TradeZone

                // Checkbox to select default parameters
                UseRenkoDefaults = true;

                // Renko Defaults
                OverBoughtRenko = 55;
                OverSoldRenko = -55;
                PercentDLengthRenko = 3;
                PercentKLengthRenko = 1;

                // Time Chart Defaults
                OverBoughtTime = 30;
                OverSoldTime = -30;
                PercentDLengthTime = 7;
                PercentKLengthTime = 5;

                // Adjusted the plots as per your request
                AddPlot(new Stroke(Brushes.LimeGreen, DashStyleHelper.Dot, 4), PlotStyle.Dot, "TradeZonePlot"); // Index 0
                AddPlot(new Stroke(Brushes.Lime, DashStyleHelper.Solid, 3), PlotStyle.Line, "TradeZoneAVGPlot"); // Index 1

                PosColor = Brushes.LimeGreen; // You can adjust this if needed
                NegColor = Brushes.Red;
                #endregion
            }
            else if (State == State.Configure)
            {
                #region Configure for TradeZone
                AddLine(Brushes.Transparent, 0, "ZeroLine");
                AddLine(Brushes.Blue, UseRenkoDefaults ? OverBoughtRenko : OverBoughtTime, "OverBoughtLine");
                AddLine(Brushes.HotPink, UseRenkoDefaults ? OverSoldRenko : OverSoldTime, "OverSoldLine");
                #endregion
            }
            else if (State == State.DataLoaded)
            {
                #region DataLoaded for BouncePoint
                lineColor = new Series<double>(this);
                flatSection = new Series<bool>(this);
                _direction = new Series<double>(this, MaximumBarsLookBack.Infinite);
                _signal = new Series<double>(this, MaximumBarsLookBack.Infinite);
                #endregion

                #region DataLoaded for TradeZone
                // Initialize components for Renko Defaults
                MinLowRenko = MIN(Low, PercentKLengthRenko);
                MaxHighRenko = MAX(High, PercentKLengthRenko);
                RelDiffRenko = new Series<double>(this, MaximumBarsLookBack.Infinite);
                DiffRenko = new Series<double>(this, MaximumBarsLookBack.Infinite);
                SMISeriesRenko = new Series<double>(this, MaximumBarsLookBack.Infinite);
                EMA0Renko = EMA(RelDiffRenko, PercentDLengthRenko);
                AvgRelRenko = EMA(EMA0Renko, PercentDLengthRenko);
                EMA1Renko = EMA(DiffRenko, PercentDLengthRenko);
                AvgDiffRenko = EMA(EMA1Renko, PercentDLengthRenko);
                SMIEMARenko = EMA(SMISeriesRenko, PercentDLengthRenko);

                // Initialize components for Time Chart Defaults
                MinLowTime = MIN(Low, PercentKLengthTime);
                MaxHighTime = MAX(High, PercentKLengthTime);
                RelDiffTime = new Series<double>(this, MaximumBarsLookBack.Infinite);
                DiffTime = new Series<double>(this, MaximumBarsLookBack.Infinite);
                SMISeriesTime = new Series<double>(this, MaximumBarsLookBack.Infinite);
                EMA0Time = EMA(RelDiffTime, PercentDLengthTime);
                AvgRelTime = EMA(EMA0Time, PercentDLengthTime);
                EMA1Time = EMA(DiffTime, PercentDLengthTime);
                AvgDiffTime = EMA(EMA1Time, PercentDLengthTime);
                SMIEMATime = EMA(SMISeriesTime, PercentDLengthTime);
                #endregion
            }
        }

        protected override void OnBarUpdate()
        {
            #region OnBarUpdate for BouncePoint
            // ARROWS
            if (CurrentBar == 0)
            {
                textFont = new SimpleFont("Wingdings 3", markersize);
                textFont1 = new SimpleFont("Wingdings 3", markersize * 0.8);//0.75
                textFont2 = new SimpleFont("Wingdings 2", markersize * 1.5);//t or u are diamonds
                textFont3 = new SimpleFont("Wingdings 3", markersize);//t or u are diamonds
            }
            if (CurrentBar < cciPeriod || CurrentBar < atrPeriod)
                return;

            cciVal = CCI(Close, cciPeriod)[0];
            atrVal = ATR(Close, atrPeriod)[0];

            upTrend = Low[0] - atrVal * atrMult;
            downTrend = High[0] + atrVal * atrMult;

            double trendValue = 0.0;

            if (cciVal >= 0)
                if (upTrend < (double)TrendSeries[1])
                    trendValue = (double)TrendSeries[1];
                else
                    trendValue = upTrend;
            else
                if (downTrend > (double)TrendSeries[1])
                trendValue = (double)TrendSeries[1];
            else
                trendValue = downTrend;

            TrendSeries[0] = trendValue;

            flatSection[0] = TrendSeries[0] == TrendSeries[1];

            _direction[0] = (TrendSeries[0] > TrendSeries[1] ? 1 :
                            TrendSeries[0] < TrendSeries[1] ? -1 : 0);

            _signal[0] = ((_direction[1] < 1 && _direction[0] > 0) || (CrossAbove(_direction, 0.5, 1) && _direction[0] > 0)) ? 1 :
                        ((_direction[1] > -1 && _direction[0] < 0) || (CrossBelow(_direction, -0.5, 1) && _direction[0] < 0)) ? -1 : 0;

            if (_signal[0] == 0)
            {
                ArrowPrintedUP = false;
                ArrowPrintedDOWN = false;
            }

            double val = 0;
            double spot = 0;

            // Draw a dot on the main chart when flatSection[0] is true
            if (flatSection[0])
            {
                Draw.Dot(this, "TrendDot" + CurrentBar, false, 0, TrendSeries[0], PlotColor);
            }

            // Early Arrow Logic - prints arrow on the FIRST dot of each new flat section
            // Determines direction from CCI: CCI >= 0 means uptrend mode (UP arrow),
            // CCI < 0 means downtrend mode (DOWN arrow). Uses only current-bar data,
            // so there is no repainting or backpainting.
            if (DrawEarlyArrows && flatSection[0] && _direction[1] != 0)
            {
                if (cciVal >= 0)
                {
                    double earlySpot = Math.Min(Low[0], TrendSeries[0]) - ArrowDisplacement * TickSize;
                    Draw.Text(this, "earlysigup" + CurrentBar, true, "h", 0, earlySpot, 0,
                        EarlyArrowUpColor, textFont, TextAlignment.Center,
                        Brushes.Transparent, Brushes.Transparent, 5);
                }
                else
                {
                    double earlySpot = Math.Max(High[0], TrendSeries[0]) + ArrowDisplacement * TickSize;
                    Draw.Text(this, "earlysigdown" + CurrentBar, true, "i", 0, earlySpot, 0,
                        EarlyArrowDownColor, textFont, TextAlignment.Center,
                        Brushes.Transparent, Brushes.Transparent, 5);
                }
            }

            if (DrawArrows)
            {
                if (!ArrowPrintedUP && _signal[0] > 0)
                {
                    val = Low[0] - ArrowDisplacement * TickSize;
                    spot = Math.Min(Low[0], TrendSeries[0]) - ArrowDisplacement * TickSize;

                    Draw.Text(this, "sigup" + (CurrentBar), true, "h"/*"l"*/, 0, spot, 0, ArrowUpColor, textFont, TextAlignment.Center, Brushes.Transparent, Brushes.Transparent, 5);
                    ArrowPrintedUP = true;
                }
                else
                if (!ArrowPrintedDOWN && _signal[0] < 0)
                {
                    val = High[0] + ArrowDisplacement * TickSize;
                    spot = Math.Max(High[0], TrendSeries[0]) + ArrowDisplacement * TickSize;

                    Draw.Text(this, "sigdown" + (CurrentBar), true, "i"/*"l"*/, 0, spot, 0, ArrowDownColor, textFont, TextAlignment.Center, Brushes.Transparent, Brushes.Transparent, 5);
                    ArrowPrintedDOWN = true;
                }
            }
            #endregion

            #region OnBarUpdate for TradeZone

            // Decide which parameters and components to use
            double OverBought = UseRenkoDefaults ? OverBoughtRenko : OverBoughtTime;
            double OverSold = UseRenkoDefaults ? OverSoldRenko : OverSoldTime;
            Series<double> RelDiff = UseRenkoDefaults ? RelDiffRenko : RelDiffTime;
            Series<double> Diff = UseRenkoDefaults ? DiffRenko : DiffTime;
            Series<double> SMISeries = UseRenkoDefaults ? SMISeriesRenko : SMISeriesTime;
            EMA EMA0 = UseRenkoDefaults ? EMA0Renko : EMA0Time;
            EMA AvgRel = UseRenkoDefaults ? AvgRelRenko : AvgRelTime;
            EMA EMA1 = UseRenkoDefaults ? EMA1Renko : EMA1Time;
            EMA AvgDiff = UseRenkoDefaults ? AvgDiffRenko : AvgDiffTime;
            EMA SMIEMA = UseRenkoDefaults ? SMIEMARenko : SMIEMATime;
            MIN MinLow = UseRenkoDefaults ? MinLowRenko : MinLowTime;
            MAX MaxHigh = UseRenkoDefaults ? MaxHighRenko : MaxHighTime;

            if (CurrentBar >= Math.Max(UseRenkoDefaults ? PercentKLengthRenko : PercentKLengthTime, UseRenkoDefaults ? PercentDLengthRenko : PercentDLengthTime))
            {
                RelDiff[0] = Close[0] - (MaxHigh[0] + MinLow[0]) / 2;
                Diff[0] = MaxHigh[0] - MinLow[0];

                if (AvgDiff[0] != 0)
                {
                    SMISeries[0] = AvgRel[0] / (AvgDiff[0] / 2) * 100;

                    // Set the colors for both plots based on the value of SMISeries[0]
                    if (SMISeries[0] > 0)
                    {
                        PlotBrushes[0][0] = PosColor; // Index 0 for TradeZonePlot
                        PlotBrushes[1][0] = PosColor; // Index 1 for TradeZoneAVGPlot
                    }
                    else
                    {
                        PlotBrushes[0][0] = NegColor;
                        PlotBrushes[1][0] = NegColor;
                    }

                    TradeZonePlot[0] = SMISeries[0];
                }
                else
                {
                    SMISeries[0] = 0;
                    TradeZonePlot[0] = SMISeries[0];
                }

                TradeZoneAVGPlot[0] = SMIEMA[0];
            }
            #endregion
        }

        public override string DisplayName
        {
            get { if (State == State.SetDefaults) return "DualTrendIndicator"; else return ""; }
        }

        #region Properties for BouncePoint

        // Since we cannot plot Trend on the main chart using standard plots, we use a Series to store Trend values
        [Browsable(false)]
        [XmlIgnore()]
        public Series<double> TrendSeries
        {
            get
            {
                if (_TrendSeries == null)
                    _TrendSeries = new Series<double>(this);
                return _TrendSeries;
            }
        }
        private Series<double> _TrendSeries;

        [Browsable(false)]
        [XmlIgnore()]
        public Series<bool> FlatSection { get { Update(); return flatSection; } }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "CCI Period", Order = 1, GroupName = "BouncePoint Parameters")]
        public int cciPeriod { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "ATR Period", Order = 2, GroupName = "BouncePoint Parameters")]
        public int atrPeriod { get; set; }

        [Range(-20, int.MaxValue)]
        [Display(Name = "ATR Multiplier", Order = 3, GroupName = "BouncePoint Parameters")]
        public double atrMult { get; set; }

        [XmlIgnore()]
        [Display(Name = "Plot Color", GroupName = "BouncePoint Colors", Order = 1)]
        public Brush PlotColor { get; set; }

        [Browsable(false)]
        public string PlotColorSerialize
        {
            get { return Serialize.BrushToString(PlotColor); }
            set { PlotColor = Serialize.StringToBrush(value); }
        }

        [Display(Name = "Draw Arrows?", GroupName = "BouncePoint Drawing Objects", Order = 1)]
        public bool DrawArrows { get; set; }

        [Display(Name = "Arrow Displacement", GroupName = "BouncePoint Drawing Objects", Order = 2)]
        public int ArrowDisplacement { get; set; }

        [XmlIgnore()]
        [Display(Name = "Color for Up Arrows?", Description = "Color for Up Arrow", GroupName = "BouncePoint Drawing Objects", Order = 3)]
        public Brush ArrowUpColor { get; set; }

        [Browsable(false)]
        public string ArrowUpColorSerialize
        {
            get { return Serialize.BrushToString(ArrowUpColor); }
            set { ArrowUpColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore()]
        [Display(Name = "Color for Down Arrows?", Description = "Color for Down Arrows", GroupName = "BouncePoint Drawing Objects", Order = 4)]
        public Brush ArrowDownColor { get; set; }

        [Browsable(false)]
        public string ArrowDownColorSerialize
        {
            get { return Serialize.BrushToString(ArrowDownColor); }
            set { ArrowDownColor = Serialize.StringToBrush(value); }
        }

        [Display(Name = "Draw Early Arrows?", Description = "Draw arrows on the first dot of each new flat section", GroupName = "BouncePoint Drawing Objects", Order = 5)]
        public bool DrawEarlyArrows { get; set; }

        [XmlIgnore()]
        [Display(Name = "Color for Early Up Arrows", Description = "Color for Up Arrow on First Dot", GroupName = "BouncePoint Drawing Objects", Order = 6)]
        public Brush EarlyArrowUpColor { get; set; }

        [Browsable(false)]
        public string EarlyArrowUpColorSerialize
        {
            get { return Serialize.BrushToString(EarlyArrowUpColor); }
            set { EarlyArrowUpColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore()]
        [Display(Name = "Color for Early Down Arrows", Description = "Color for Down Arrow on First Dot", GroupName = "BouncePoint Drawing Objects", Order = 7)]
        public Brush EarlyArrowDownColor { get; set; }

        [Browsable(false)]
        public string EarlyArrowDownColorSerialize
        {
            get { return Serialize.BrushToString(EarlyArrowDownColor); }
            set { EarlyArrowDownColor = Serialize.StringToBrush(value); }
        }

        #endregion

        #region Properties for TradeZone

        // Checkbox to select default parameters
        [NinjaScriptProperty]
        [Display(Name = "Use Renko Defaults?", Order = 0, GroupName = "TradeZone Parameters")]
        public bool UseRenkoDefaults { get; set; }

        // Renko Defaults
        [NinjaScriptProperty]
        [Range(double.MinValue, double.MaxValue)]
        [Display(Name = "OverBought Renko", Order = 1, GroupName = "TradeZone Parameters (Renko)")]
        public double OverBoughtRenko { get; set; }

        [NinjaScriptProperty]
        [Range(double.MinValue, double.MaxValue)]
        [Display(Name = "OverSold Renko", Order = 2, GroupName = "TradeZone Parameters (Renko)")]
        public double OverSoldRenko { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "PercentDLength Renko", Order = 3, GroupName = "TradeZone Parameters (Renko)")]
        public int PercentDLengthRenko { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "PercentKLength Renko", Order = 4, GroupName = "TradeZone Parameters (Renko)")]
        public int PercentKLengthRenko { get; set; }

        // Time Chart Defaults
        [NinjaScriptProperty]
        [Range(double.MinValue, double.MaxValue)]
        [Display(Name = "OverBought Time", Order = 5, GroupName = "TradeZone Parameters (Time Chart)")]
        public double OverBoughtTime { get; set; }

        [NinjaScriptProperty]
        [Range(double.MinValue, double.MaxValue)]
        [Display(Name = "OverSold Time", Order = 6, GroupName = "TradeZone Parameters (Time Chart)")]
        public double OverSoldTime { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "PercentDLength Time", Order = 7, GroupName = "TradeZone Parameters (Time Chart)")]
        public int PercentDLengthTime { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "PercentKLength Time", Order = 8, GroupName = "TradeZone Parameters (Time Chart)")]
        public int PercentKLengthTime { get; set; }

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> TradeZonePlot
        {
            get { return Values[0]; } // Index 0
        }

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> TradeZoneAVGPlot
        {
            get { return Values[1]; } // Index 1
        }

        [XmlIgnore]
        [Display(Name = "Positive Color", GroupName = "TradeZone Colors", Order = 1)]
        public Brush PosColor { get; set; }

        [Browsable(false)]
        public string PosSerialize
        {
            get { return Serialize.BrushToString(PosColor); }
            set { PosColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Negative Color", GroupName = "TradeZone Colors", Order = 2)]
        public Brush NegColor { get; set; }

        [Browsable(false)]
        public string NegSerialize
        {
            get { return Serialize.BrushToString(NegColor); }
            set { NegColor = Serialize.StringToBrush(value); }
        }

        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private PropTraderz.DualTrendIndicator[] cacheDualTrendIndicator;
		public PropTraderz.DualTrendIndicator DualTrendIndicator(int cciPeriod, int atrPeriod, bool useRenkoDefaults, double overBoughtRenko, double overSoldRenko, int percentDLengthRenko, int percentKLengthRenko, double overBoughtTime, double overSoldTime, int percentDLengthTime, int percentKLengthTime)
		{
			return DualTrendIndicator(Input, cciPeriod, atrPeriod, useRenkoDefaults, overBoughtRenko, overSoldRenko, percentDLengthRenko, percentKLengthRenko, overBoughtTime, overSoldTime, percentDLengthTime, percentKLengthTime);
		}

		public PropTraderz.DualTrendIndicator DualTrendIndicator(ISeries<double> input, int cciPeriod, int atrPeriod, bool useRenkoDefaults, double overBoughtRenko, double overSoldRenko, int percentDLengthRenko, int percentKLengthRenko, double overBoughtTime, double overSoldTime, int percentDLengthTime, int percentKLengthTime)
		{
			if (cacheDualTrendIndicator != null)
				for (int idx = 0; idx < cacheDualTrendIndicator.Length; idx++)
					if (cacheDualTrendIndicator[idx] != null && cacheDualTrendIndicator[idx].cciPeriod == cciPeriod && cacheDualTrendIndicator[idx].atrPeriod == atrPeriod && cacheDualTrendIndicator[idx].UseRenkoDefaults == useRenkoDefaults && cacheDualTrendIndicator[idx].OverBoughtRenko == overBoughtRenko && cacheDualTrendIndicator[idx].OverSoldRenko == overSoldRenko && cacheDualTrendIndicator[idx].PercentDLengthRenko == percentDLengthRenko && cacheDualTrendIndicator[idx].PercentKLengthRenko == percentKLengthRenko && cacheDualTrendIndicator[idx].OverBoughtTime == overBoughtTime && cacheDualTrendIndicator[idx].OverSoldTime == overSoldTime && cacheDualTrendIndicator[idx].PercentDLengthTime == percentDLengthTime && cacheDualTrendIndicator[idx].PercentKLengthTime == percentKLengthTime && cacheDualTrendIndicator[idx].EqualsInput(input))
						return cacheDualTrendIndicator[idx];
			return CacheIndicator<PropTraderz.DualTrendIndicator>(new PropTraderz.DualTrendIndicator(){ cciPeriod = cciPeriod, atrPeriod = atrPeriod, UseRenkoDefaults = useRenkoDefaults, OverBoughtRenko = overBoughtRenko, OverSoldRenko = overSoldRenko, PercentDLengthRenko = percentDLengthRenko, PercentKLengthRenko = percentKLengthRenko, OverBoughtTime = overBoughtTime, OverSoldTime = overSoldTime, PercentDLengthTime = percentDLengthTime, PercentKLengthTime = percentKLengthTime }, input, ref cacheDualTrendIndicator);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.PropTraderz.DualTrendIndicator DualTrendIndicator(int cciPeriod, int atrPeriod, bool useRenkoDefaults, double overBoughtRenko, double overSoldRenko, int percentDLengthRenko, int percentKLengthRenko, double overBoughtTime, double overSoldTime, int percentDLengthTime, int percentKLengthTime)
		{
			return indicator.DualTrendIndicator(Input, cciPeriod, atrPeriod, useRenkoDefaults, overBoughtRenko, overSoldRenko, percentDLengthRenko, percentKLengthRenko, overBoughtTime, overSoldTime, percentDLengthTime, percentKLengthTime);
		}

		public Indicators.PropTraderz.DualTrendIndicator DualTrendIndicator(ISeries<double> input , int cciPeriod, int atrPeriod, bool useRenkoDefaults, double overBoughtRenko, double overSoldRenko, int percentDLengthRenko, int percentKLengthRenko, double overBoughtTime, double overSoldTime, int percentDLengthTime, int percentKLengthTime)
		{
			return indicator.DualTrendIndicator(input, cciPeriod, atrPeriod, useRenkoDefaults, overBoughtRenko, overSoldRenko, percentDLengthRenko, percentKLengthRenko, overBoughtTime, overSoldTime, percentDLengthTime, percentKLengthTime);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.PropTraderz.DualTrendIndicator DualTrendIndicator(int cciPeriod, int atrPeriod, bool useRenkoDefaults, double overBoughtRenko, double overSoldRenko, int percentDLengthRenko, int percentKLengthRenko, double overBoughtTime, double overSoldTime, int percentDLengthTime, int percentKLengthTime)
		{
			return indicator.DualTrendIndicator(Input, cciPeriod, atrPeriod, useRenkoDefaults, overBoughtRenko, overSoldRenko, percentDLengthRenko, percentKLengthRenko, overBoughtTime, overSoldTime, percentDLengthTime, percentKLengthTime);
		}

		public Indicators.PropTraderz.DualTrendIndicator DualTrendIndicator(ISeries<double> input , int cciPeriod, int atrPeriod, bool useRenkoDefaults, double overBoughtRenko, double overSoldRenko, int percentDLengthRenko, int percentKLengthRenko, double overBoughtTime, double overSoldTime, int percentDLengthTime, int percentKLengthTime)
		{
			return indicator.DualTrendIndicator(input, cciPeriod, atrPeriod, useRenkoDefaults, overBoughtRenko, overSoldRenko, percentDLengthRenko, percentKLengthRenko, overBoughtTime, overSoldTime, percentDLengthTime, percentKLengthTime);
		}
	}
}

#endregion
