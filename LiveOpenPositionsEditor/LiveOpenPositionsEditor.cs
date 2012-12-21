using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using RightEdge.Common;
using RightEdge.Common.Internal;
using System.Reflection;

namespace RightEdge.Util
{
	public class LiveOpenPositionsEditor
	{
		public PortfolioXml PortfolioXml { get; set; }
        Dictionary<Symbol, PositionDataXml> _longBrokerPositions = new Dictionary<Symbol, PositionDataXml>();
        Dictionary<Symbol, PositionDataXml> _shortBrokerPositions = new Dictionary<Symbol, PositionDataXml>();

		public LiveOpenPositionsEditor()
		{
			PortfolioXml = new PortfolioXml();
		}

		public LiveOpenPositionsEditor(string path)
		{
			PortfolioXml = PositionManager.LoadOpenPositions(path);
            _longBrokerPositions = new Dictionary<Symbol, PositionDataXml>();
            _shortBrokerPositions = new Dictionary<Symbol, PositionDataXml>();
            foreach (var brokerPos in PortfolioXml.BrokerPositions)
            {
                var position = GetBrokerPosition(brokerPos.Symbol, brokerPos.Direction);
                TradeInfo trade = new TradeInfo();
                trade.FilledTime = brokerPos.EntryDate;
                trade.TransactionType = brokerPos.Direction == PositionType.Long ? TransactionType.Buy : TransactionType.Sell;
                trade.Price = brokerPos.EntryPrice;
                trade.Size = brokerPos.Size;
                trade.OrderType = OrderType.Market;
                trade.TradeType = TradeType.OpenPosition;

                position.Trades.Add(trade);

            }
		}

		public void Save(string path)
		{
            PortfolioXml.BrokerPositions = new List<BrokerPosition>();
            foreach (var pos in _longBrokerPositions.Values.Concat(_shortBrokerPositions.Values))
            {
                BrokerPosition brokerPosition = CreateBrokerPosition(pos);
                PortfolioXml.BrokerPositions.Add(brokerPosition);
            }

			XmlWriterSettings writeSettings = new XmlWriterSettings();
			writeSettings.CloseOutput = true;
			writeSettings.Indent = true;

			using (XmlWriter tw = XmlWriter.Create(path, writeSettings))
			{
				XmlSerializer xmlSerializer = new XmlSerializer(typeof(PortfolioXml));
				xmlSerializer.Serialize(tw, PortfolioXml);
			}
		}

		public PositionDataXml AddPosition(Symbol symbol, PositionType positionType)
		{
            var newPosition = CreatePositionDataXml(symbol, positionType);

			PortfolioXml.Positions.Add(newPosition);

			return newPosition;
		}

        public TradeInfo AddCompletedTradeToPosition(PositionDataXml position, DateTime filledTime, TransactionType transactionType, Price price, long size, string orderID)
        {
            PositionDataXml brokerPos = GetBrokerPosition(position.Symbol, position.PositionType);

            //  Create a PositionInfo object to calculate the PositionStats, which we can use to calculate a new TradeInfo
            PositionInfo brokerPosInfo = GetPositionInfo(brokerPos);

            BrokerOrder order = new BrokerOrder();
            order.OrderId = orderID;
            order.PositionID = position.PosID;
            order.TransactionType = transactionType;
            order.OrderType = OrderType.Market;
            order.OrderSymbol = position.Symbol;

            Fill fill = new Fill();
            fill.FillDateTime = filledTime;
            fill.Price = price;
            fill.Quantity = size;
            fill.Commission = 0;

            var createPositionTradeMethod = typeof(PositionManager).GetMethod("CreatePositionTrade", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            var argList = new object[]
            {
                position.Symbol,
                brokerPosInfo.CurrentStats,
                new SymbolAccountInfo(position.Symbol),
                order,
                fill,
                TradeType.UserSubmitted,        //  Trade type
                string.Empty                    //  Description
            };

            TradeInfo ret = (TradeInfo)createPositionTradeMethod.Invoke(null, argList);

            if (position.Trades.Any(t => t.FilledTime == filledTime))
            {
                ret.Sequence = position.Trades.Where(t => t.FilledTime == filledTime).Max(t => t.Sequence) + 1;
            }
            else
            {
                ret.Sequence = 0;
            }

            position.Trades.Add(ret);
            brokerPos.Trades.Add(ret);

            PortfolioXml.CurrentPrices[position.Symbol] = price.SymbolPrice;

            position.IsPending = false;
            brokerPos.IsPending = false;

            //  If position size is now zero, remove it from the list of open positions
            if (GetPositionInfo(position).CurrentStats.CurrentSize == 0)
            {
                PortfolioXml.Positions.Remove(position);
            }
            if (GetPositionInfo(brokerPos).CurrentStats.CurrentSize == 0)
            {
                if (brokerPos.PositionType == PositionType.Long)
                {
                    _longBrokerPositions.Remove(brokerPos.Symbol);
                }
                else
                {
                    _shortBrokerPositions.Remove(brokerPos.Symbol);
                }
            }

            return ret;
        }

        public TradeOrderXml AddPendingOrder(PositionDataXml position, BrokerOrder brokerOrder, string orderId, long size, DateTime submittedTime, OrderType orderType, TransactionType transactionType)
        {
            brokerOrder.OrderSymbol = position.Symbol;
            brokerOrder.OrderId = orderId;
            brokerOrder.PositionID = position.PosID;
            brokerOrder.Shares = size;
            brokerOrder.SubmittedDate = submittedTime;
            brokerOrder.OrderType = orderType;
            brokerOrder.TransactionType = transactionType;
            brokerOrder.OrderState = BrokerOrderState.Submitted;

            TradeOrderXml ret = new TradeOrderXml();
            ret.PosID = position.PosID;
            ret.OrderID = orderId;
            ret.TradeType = TradeType.UserSubmitted;
            ret.Description = string.Empty;
            ret.Error = null;
            ret.BarsValid = -1;
            ret.CancelPending = false;

            PortfolioXml.PendingOrders.Add(brokerOrder);
            position.PendingOrders.Add(ret);
            return ret;
        }
        

        PositionDataXml GetBrokerPosition(Symbol symbol, PositionType direction)
        {
            Dictionary<Symbol, PositionDataXml> dict;
            if (direction == PositionType.Long)
            {
                dict = _longBrokerPositions;
            }
            else if (direction == PositionType.Short)
            {
                dict = _shortBrokerPositions;
            }
            else
            {
                throw new RightEdgeError("Unexpected position type: " + direction);
            }

            PositionDataXml brokerPosition;
            if (dict.TryGetValue(symbol, out brokerPosition))
            {
                return brokerPosition;
            }
            //if (!create)
            //{
            //    return null;
            //}

            brokerPosition = CreatePositionDataXml(symbol, direction);
            brokerPosition.PosID = "b" + brokerPosition.PosID;

            dict[symbol] = brokerPosition;

            return brokerPosition;
        }

        PositionDataXml CreatePositionDataXml(Symbol symbol, PositionType positionType)
        {
            PositionDataXml newPosition = new PositionDataXml();
            newPosition.PosID = GetNextPositionID();
            newPosition.Symbol = symbol;
            newPosition.PositionType = positionType;
            newPosition.Trades = new List<TradeInfo>();
            newPosition.PendingOrders = new List<TradeOrderXml>();
            newPosition.IsPending = true;
            newPosition.BarCountExit = -1;

            return newPosition;
        }

        BrokerPosition CreateBrokerPosition(PositionDataXml pos)
        {
            PositionInfo info = GetPositionInfo(pos);

            BrokerPosition brokerPos = new BrokerPosition();
            brokerPos.Symbol = pos.Symbol;
            brokerPos.Direction = pos.PositionType;
            brokerPos.Size = info.CurrentStats.CurrentSize;
            brokerPos.EntryPrice = info.CurrentStats.EntryPrice;
            brokerPos.EntryDate = info.OpenDate;

            return brokerPos;
        }

        PositionInfo GetPositionInfo(PositionDataXml position)
        {
            PositionInfo info = new PositionInfo();
            //info.PosID = position.PosID;
            typeof(PositionInfo).GetProperty("PosID").SetValue(info, position.PosID, null);
            //info.Symbol = position.Symbol;
            typeof(PositionInfo).GetProperty("Symbol").SetValue(info, position.Symbol, null);
            info.PositionType = position.PositionType;
            //info.Description = position.Description;
            typeof(PositionInfo).GetProperty("Description").SetValue(info, position.Description, null);

            foreach (TradeInfo tradeInfo in position.Trades)
            {
                info.Trades.Add(tradeInfo);
            }

            return info;
        }

		string GetNextPositionID()
		{
			int nextID = 1;
			foreach (var position in PortfolioXml.Positions)
			{
                string idString = position.PosID;
                if (idString.ToLower().StartsWith("b"))
                {
                    idString = idString.Substring(1);
                }

				int id;
				if (int.TryParse(idString, out id))
				{
					nextID = Math.Max(nextID, id + 1);
				}
			}

			return nextID.ToString();
		}
	}
}
