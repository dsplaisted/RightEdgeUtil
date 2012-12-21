using RightEdge.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RightEdge.Util
{
    class SymbolAccountInfo : IAccountInfo
    {
        Symbol _symbol;

        public SymbolAccountInfo(Symbol symbol)
        {
            _symbol = symbol;
        }

        public CurrencyType AccountCurrency
        {
            get { return _symbol.CurrencyType; }
        }

        public bool AppliesForexInterest
        {
            get { return false; }
        }

        public TimeSpan ForexRolloverTime
        {
            get { return TimeSpan.Zero; }
        }

        public ReturnValue<double> GetConversionRate(CurrencyType source, CurrencyType dest, QuoteType type)
        {
            throw new NotImplementedException();
        }

        public ReturnValue<double> GetInterestRate(CurrencyType currency, QuoteType type)
        {
            throw new NotImplementedException();
        }

        public bool SubjectToCurrencyRisk(Symbol symbol)
        {
            if (symbol.AssetClass == AssetClass.Forex)
            {
                return false;
            }
            else if (symbol.AssetClass == AssetClass.Future ||
                symbol.AssetClass == AssetClass.Option ||
                symbol.AssetClass == AssetClass.FuturesOption)
            {
                if (symbol.SymbolInformation.Margin > 0)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
