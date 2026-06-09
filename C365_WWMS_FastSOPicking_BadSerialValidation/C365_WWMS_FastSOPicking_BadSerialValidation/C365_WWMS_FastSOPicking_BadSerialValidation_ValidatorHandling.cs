using System;
using System.Data;
using P21.Extensions.BusinessRule;

namespace C365_WWMS_FastSOPicking_BadSerialValidation
{
    public class C365_WWMS_FastSOPicking_BadSerialValidation_ValidatorHandling : P21.Extensions.BusinessRule.Rule
    {
        private const string TableName = "d_dw_rf_fast_sales_order_picking_item";
        private const string SerialNoField = "c_rf_serial_no";

        public override RuleResult Execute()
        {
            RuleResult result = new RuleResult() { Success = true };

            try
            {
                if (RuleState.IsCallbackRule)
                {
                    string selectedButtonName = Data.Set.Tables["response_data"].Rows[0].Field<string>("selected_button_name");

                    if (selectedButtonName == "Nobutton")
                    {
                        // Block the transaction -- do NOT fire Enter
                        // Let P21 reject the entry and return focus to the serial field
                        result.Success = false;
                        result.Message = "Invalid serial number. Please re-scan or enter a valid serial number.";
                    }
                    else if (selectedButtonName == "Yesbutton")
                    {
                        // Allow through and simulate Enter
                        result.Keystroke = "Enter";
                        result.Success = true;
                    }
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = "Callback rule error: " + ex.Message + Environment.NewLine + ex.ToString();
            }

            return result;
        }

        public override string GetName() =>
            "C365_WWMS_FastSOPicking_BadSerialValidation_ValidatorHandling";

        public override string GetDescription() =>
            "Handles the Yes/No callback response from the Bad Serial Number Validation popup. " +
            "Yes: allows the serial through and fires Enter. " +
            "No: blocks the transaction and returns focus for re-scan.";
    }
}
