using RightEdge.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RightEdge.Util
{
    public class ActivityLogger
    {
        SystemData _systemData;
        SystemLogger _systemLogger;

        public ActivityLogger(SystemData systemData, SystemLogger systemLogger)
        {
            _systemData = systemData;
            _systemLogger = systemLogger;

            _systemData.PositionManager.OrderSubmitted += new EventHandler<OrderUpdatedEventArgs>(PositionManager_OrderSubmitted);
            _systemData.PositionManager.OrderFilled += new EventHandler<OrderFilledEventArgs>(PositionManager_OrderFilled);
            _systemData.PositionManager.OrderUpdated += new EventHandler<OrderUpdatedEventArgs>(PositionManager_OrderUpdated);
            _systemData.PositionManager.SpecialOrderFailed += new EventHandler<OrderUpdatedEventArgs>(PositionManager_SpecialOrderFailed);

        }

        private void PositionManager_OrderSubmitted(object sender, OrderUpdatedEventArgs e)
        {
            string message = "Order Submitted: " + e.Order.ToString();
            _systemLogger.Log(OutputSeverityLevel.Informational, "ActivityLogger", e.Order.Symbol, message);
        }

        private void PositionManager_OrderFilled(object sender, OrderFilledEventArgs e)
        {
            string message = "Filled " + e.Trade.Size + " @" + e.Trade.Price + " for order " + e.Trade.Order.ToString();
            _systemLogger.Log(OutputSeverityLevel.Informational, "ActivityLogger", e.Position.Symbol, message);
        }

        private void PositionManager_OrderUpdated(object sender, OrderUpdatedEventArgs e)
        {
            if (e.Order.OrderState == BrokerOrderState.Cancelled ||
                e.Order.OrderState == BrokerOrderState.Rejected)
            {
                string message = "Order " + e.Order.OrderState.ToString() + ":" + e.Order.ToString();
                if (!string.IsNullOrEmpty(e.Information))
                {
                    message += " Additional information: " + e.Information;
                }

                OutputSeverityLevel severityLevel = e.Order.CancelPending ? OutputSeverityLevel.Informational : OutputSeverityLevel.Warning;

                _systemLogger.Log(severityLevel, "ActivityLogger", e.Order.Symbol, message);
            }
            else
            {
                string message = "Order updated: " + e.Order.ToString();
                if (!string.IsNullOrEmpty(e.Information))
                {
                    message += " Additional information: " + e.Information;
                }

                _systemLogger.Log(OutputSeverityLevel.Informational, "ActivityLogger", e.Order.Symbol, message);
            }
        }

        private void PositionManager_SpecialOrderFailed(object sender, OrderUpdatedEventArgs e)
        {
            PositionManager_OrderUpdated(sender, e);
        }
    }
}
