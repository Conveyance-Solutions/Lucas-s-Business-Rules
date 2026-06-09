using System;
using System.Data;
using System.Text.RegularExpressions;
using P21.Extensions.BusinessRule;

namespace C365_WWMS_FastSOPicking_BadSerialValidation
{
    public class C365_WWMS_FastSOPicking_BadSerialValidation : P21.Extensions.BusinessRule.Rule
    {
        private const string TableName = "d_dw_rf_fast_sales_order_picking_item";
        private const string SerialNoField = "c_rf_serial_no";
        private const string ItemIdField = "c_rf_item_id";

        public override RuleResult Execute()
        {
            RuleResult result = new RuleResult() { Success = true };

            try
            {
                DataRow row = Data.Set.Tables[TableName].Rows[0];

                string serialNo = row[SerialNoField] == DBNull.Value ? string.Empty : row[SerialNoField].ToString().Trim();
                string itemId = row[ItemIdField] == DBNull.Value ? string.Empty : row[ItemIdField].ToString().Trim();

                // Nothing entered -- let P21 handle blank validation natively
                if (string.IsNullOrWhiteSpace(serialNo))
                {
                    result.Success = true;
                    return result;
                }

                // Check for any character that is NOT alphanumeric (A-Z, a-z, 0-9)
                bool hasBadChars = Regex.IsMatch(serialNo, @"[^A-Za-z0-9]");

                if (!hasBadChars)
                {
                    // Serial is clean -- pass through and simulate Enter
                    result.Success = true;
                    result.Keystroke = "Enter";
                    return result;
                }

                // Bad serial -- show Yes/No popup
                ResponseAttributes responseAttributes = new ResponseAttributes();
                responseAttributes.ResponseTitle = "Serial Number Validation";
                responseAttributes.ResponseText = "Serial number " + serialNo + " for item " + itemId
                                                 + " does not pass validation (contains spaces or special characters)."
                                                 + "\r\nDo you wish to continue?";
                responseAttributes.CallbackRule = "C365_WWMS_FastSOPicking_BadSerialValidation_ValidatorHandling";

                responseAttributes.Buttons = new ResponseButton[]
                {
                    new ResponseButton("Yesbutton", "Yes", "Yes"),
                    new ResponseButton("Nobutton",  "No",  "No")
                };

                result.Success = true;
                result.ShowResponse = true;
                result.ResponseAttributes = responseAttributes;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = "Error in C365_WWMS_FastSOPicking_BadSerialValidation: " + ex.Message;
            }

            return result;
        }

        public override string GetName() =>
            "C365_WWMS_FastSOPicking_BadSerialValidation";

        public override string GetDescription() =>
            "Validates the serial number field on RF Fast Sales Order Picking. " +
            "Fires on serial number field edit. Blocks entry of serial numbers containing " +
            "spaces or special characters, prompting the user to confirm or re-enter.";
    }
}
