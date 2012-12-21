using RightEdge.Common;
using RightEdge.Common.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RightEdge.Util
{
	public class Program
	{
		public static void Main(string[] args)
		{
            string positionDataFilename;
            if (args.Length > 0)
            {
                positionDataFilename = args[0];
            }
            else
            {
                positionDataFilename = @"C:\RightEdge\Trading Systems\LiveTest\LiveOpenPositions.xml";
            }

            var openPositionData = new LiveOpenPositionsEditor(positionDataFilename);

            Symbol MSFT = new Symbol("MSFT");

            //  Sample of how to record a filled order.  If the order wasn't already tracked as a pending order, a new position will be created
            AddFilledOrder(openPositionData, orderID: "42", symbol: MSFT, direction: PositionType.Long, transactionType: TransactionType.Buy,
                fillPrice: 28.5, fillSize: 500, fillTime: new DateTime(2012, 12, 15).AddHours(10).AddMinutes(45), customString: "CUSTOM STRING");


            //  Sample of how to record a pending order.  If there is an existing position for the symbol, it will be added to that position.
            //  If there are multiple existing positions for the symbol, one of them will be picked arbitrarily.
            AddPendingOrder(openPositionData, symbol: MSFT, orderId: "43", size: 500, submittedTime: new DateTime(2012, 12, 16).AddHours(14),
                orderType: OrderType.Limit, transactionType: TransactionType.Sell, price: 55, customString: "ORDER CUSTOM STRING");

            //  When we're done, save everything back to the LiveOpenPositions.xml file
            openPositionData.Save(positionDataFilename);
		}

        //  Order ID
        //  Transaction type
        static void AddFilledOrder(LiveOpenPositionsEditor openPositionData, string orderID, Symbol symbol, PositionType direction, TransactionType transactionType, double fillPrice, long fillSize, DateTime fillTime, string customString)
        {
            if (openPositionData.PortfolioXml.Positions.SelectMany(pos => pos.Trades).Any(trade => trade.OrderID == orderID))
            {
                //  Trade already recorded
                return;
            }

            PositionDataXml position;

            BrokerOrder existingOrder = openPositionData.PortfolioXml.PendingOrders.FirstOrDefault(order => order.OrderId == orderID);
            if (existingOrder != null)
            {
                //  Order was pending, remove it
                PositionDataXml existingPosition = openPositionData.PortfolioXml.Positions.Single(p => p.PosID == existingOrder.PositionID);
                TradeOrderXml existingTradeOrder = existingPosition.PendingOrders.Single(to => to.OrderID == existingOrder.OrderId);
                existingPosition.PendingOrders.Remove(existingTradeOrder);
                openPositionData.PortfolioXml.PendingOrders.Remove(existingOrder);

                position = existingPosition;
            }
            else
            {
                position = openPositionData.AddPosition(symbol, direction);
                position.CustomString = customString;
            }

            openPositionData.AddCompletedTradeToPosition(position, fillTime, transactionType, new Price(fillPrice, fillPrice), fillSize, orderID);
        }

        static void AddPendingOrder(LiveOpenPositionsEditor openPositionData, Symbol symbol, string orderId, long size, DateTime submittedTime,
            OrderType orderType, TransactionType transactionType, double price, string customString)
        {
            if (openPositionData.PortfolioXml.PendingOrders.Any(o => o.OrderId == orderId))
            {
                //  Order already tracked
                return;
            }

            PositionType positionType = (transactionType == TransactionType.Buy || transactionType == TransactionType.Sell) ? PositionType.Long : PositionType.Short;

            //  This assumes there is just one position per symbol.  If this isn't the case then you will need to find a way of figuring out which
            //  position a pending order corresponds to.
            PositionDataXml position = openPositionData.PortfolioXml.Positions.FirstOrDefault(pos => pos.Symbol.Equals(symbol) && pos.PositionType == positionType);

            if (position == null)
            {
                //  No existing position, so create a new one
                position = openPositionData.AddPosition(symbol, positionType);
                position.CustomString = customString;
            }

            BrokerOrder brokerOrder = new BrokerOrder();
            if (orderType == OrderType.Limit || orderType == OrderType.LimitOnClose)
            {
                brokerOrder.LimitPrice = price;
            }
            else if (orderType == OrderType.Stop || orderType == OrderType.TrailingStop)
            {
                brokerOrder.StopPrice = price;
            }
            brokerOrder.CustomString = customString;

            TradeOrderXml tradeOrder = openPositionData.AddPendingOrder(position, brokerOrder, orderId, size, submittedTime, orderType, transactionType);
            
        }
	}
}
