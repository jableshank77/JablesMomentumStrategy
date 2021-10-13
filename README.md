# JablesMomentumStrategy
- The strategy will enter into a long position afer two consecutive higher closes with a stop loss initially
set at the low of the signal bar. The stop will then move to breakeven + 2 ticks after price moves x number
of ticks in the traders' favor and then will begin to trail after target 1 is met.

- The strategy will enter into a short position afer two consecutive lower closes with a stop loss initially
set at the high of the signal bar. The stop will then move to breakeven + 2 ticks after price moves x number
of ticks in the traders' favor and then will begin to trail after target 1 is met.