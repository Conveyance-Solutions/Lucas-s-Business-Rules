using System;
using System.Data;
using P21.Extensions.BusinessRule;

namespace C365_WWMS_Suppress_Message_8376
{
    public class C365_WWMS_Suppress_Message_8376 : P21.Extensions.BusinessRule.Rule
    {
        public override RuleResult Execute()
        {
            RuleResult result = new RuleResult();

            try
            {
                foreach (DataRow message in Data.Set.Tables["MessageBoxData"].Rows)
                {
                    if (message["message_no"].ToString() == "11124")
                    {
                        message["default_button"] = 1;    // Select Yes
                        message["suppress_message"] = "Y";  // Suppress popup
                        return result;
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = ex.Message + Environment.NewLine + ex.ToString();
            }

            return result;
        }

        public override string GetDescription()
        {
            return "Respond to P21 message 11124. Default and select Yes, suppress popup.";
        }

        public override string GetName()
        {
            return "C365_WWMS_Suppress_Message_8376";
        }
    }
}