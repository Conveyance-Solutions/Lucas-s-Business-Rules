using System;
using System.Data;
using System.Data.SqlClient;
using P21.Extensions.BusinessRule;

namespace C365_ScanPack_DeliveryInstructions
{
    /*
        ============================================================
        BUSINESS RULE: C365_ScanPack_DeliveryInstructions
        ============================================================

        PURPOSE
        -------
        Pull delivery instructions from the Pick Ticket (oe_pick_ticket.instructions)
        and write them into a Scan & Pack Data Entry UFC field so the packer/shipping
        user can see order-specific delivery notes while processing.

        WHAT IT READS
        -------------
        Source DataWindow table (in Data.Set):
            d_dw_scan_pack_all_shipping_contexts

        Required source column:
            pick_ticket_no

        WHAT IT WRITES
        --------------
        Target DataWindow table (in Data.Set):
            d_dw_scan_pack_dataentry

        Target UFC column:
            ufc_scan_pack_ud_delivery_instructions

        HOW IT WORKS (High-level)
        -------------------------
        1) Read the current pick_ticket_no from the shipping contexts DataWindow.
        2) Query oe_pick_ticket.instructions for that pick ticket.
        3) If no instructions exist (null/blank), use a friendly default message.
        4) Write the resulting string into the data entry DataWindow UFC field.

        WORKBENCH / SCAN & PACK NOTES
        -----------------------------
        - This rule assumes that both the SourceWindow and TargetWindow tables exist
          in Data.Set during execution. If TargetWindow is not present in the current
          trigger context, the write will be safely skipped by SetStringInSet().
        - Uses NOLOCK to reduce blocking (tradeoff: potential dirty reads).
        - Uses Row[0] as the "current row" pattern commonly used in Workbench rules.

        DEFAULT BEHAVIOR
        ----------------
        If instructions are missing or whitespace, the UFC is populated with:
            "No Delivery Instructions for this Order"
        so users always see a deterministic message instead of a blank field.
    */
    public class C365_ScanPack_DeliveryInstructions : P21.Extensions.BusinessRule.Rule
    {
        // ------------------------------------------------------------
        // SOURCE window: where the rule reads the pick ticket number.
        // ------------------------------------------------------------
        private const string SourceWindow = "d_dw_scan_pack_all_shipping_contexts";
        private const string PickTicketCol = "pick_ticket_no";

        // ------------------------------------------------------------
        // TARGET window: where the rule writes the UFC instructions field.
        // ------------------------------------------------------------
        private const string TargetWindow = "d_dw_scan_pack_dataentry";
        private const string DeliveryInstructionsCol = "ufc_scan_pack_ud_delivery_instructions";

        // Default message displayed when oe_pick_ticket.instructions is null/blank
        private const string DefaultNoInstructions = "No Delivery Instructions for this Order";

        public override RuleResult Execute()
        {
            // Default result: succeed silently, no response dialog.
            // This rule is informational (populate a field) and should not block workflow.
            var result = new RuleResult
            {
                Success = true,
                ShowResponse = false
            };

            try
            {
                // ------------------------------------------------------------
                // STEP 1: Read pick_ticket_no from the source DataWindow row.
                // ------------------------------------------------------------
                // If missing/not parseable, do nothing and return success.
                // (Common when rule triggers before pick_ticket_no is set.)
                int pickTicketNo = GetIntFromSet(SourceWindow, PickTicketCol);
                if (pickTicketNo <= 0)
                    return result;

                // ------------------------------------------------------------
                // STEP 2: Fetch delivery instructions from the database.
                // ------------------------------------------------------------
                // Reads oe_pick_ticket.instructions. Returns a default message if empty.
                string instructions = GetDeliveryInstructions(pickTicketNo);

                // ------------------------------------------------------------
                // STEP 3: Write the instructions into the Data Entry window UFC.
                // ------------------------------------------------------------
                // NOTE: This writes to the *dataentry* DataWindow table, not the source table.
                // If the TargetWindow table is not present in Data.Set for this trigger context,
                // SetStringInSet() will safely no-op (no exception).
                SetStringInSet(TargetWindow, DeliveryInstructionsCol, instructions);

                return result;
            }
            catch (Exception ex)
            {
                // If anything fails (SQL connectivity, schema mismatch, etc.),
                // return Success=false so the user/admin sees the error message.
                return new RuleResult
                {
                    Success = false,
                    Message = $"Error in C365_ScanPack_DeliveryInstructions: {ex.Message}"
                };
            }
        }

        // ============================================================
        // Database lookup: oe_pick_ticket.instructions
        // ============================================================
        /*
            GetDeliveryInstructions(pickTicketNo)
            ------------------------------------
            Reads the delivery instructions stored on the pick ticket header.

            SOURCE:
              oe_pick_ticket.instructions

            RETURNS:
              - DefaultNoInstructions if:
                  * instructions is NULL
                  * instructions is empty/whitespace
              - Otherwise returns the instruction text as-is.
        */
        private string GetDeliveryInstructions(int pickTicketNo)
        {
            const string sql = @"
SELECT oep.instructions
FROM oe_pick_ticket oep WITH (NOLOCK)
WHERE oep.pick_ticket_no = @pick_ticket_no;";

            // P21SqlConnection is provided by the base business rule class.
            // We ensure it is open before running the command.
            SqlConnection con = P21SqlConnection;
            if (con.State != ConnectionState.Open)
                con.Open();

            using (var cmd = new SqlCommand(sql, con))
            {
                // Parameterized query prevents SQL injection and improves plan reuse.
                cmd.Parameters.Add("@pick_ticket_no", SqlDbType.Int).Value = pickTicketNo;

                // ExecuteScalar returns the first column of the first row, or null.
                object val = cmd.ExecuteScalar();

                // If there is no row or the column is NULL, return default message.
                if (val == null || val == DBNull.Value)
                    return DefaultNoInstructions;

                string instructions = val.ToString();

                // Normalize empty/whitespace to the default message for deterministic UI.
                return string.IsNullOrWhiteSpace(instructions)
                    ? DefaultNoInstructions
                    : instructions;
            }
        }

        // ============================================================
        // Data.Set helpers (safe read/write to Workbench DataWindows)
        // ============================================================
        /*
            Workbench passes Data.Set (DataSet) containing one or more DataTables
            representing DataWindows in the current context.

            These helpers:
            - Guard against missing tables/columns/rows (common with different triggers)
            - Avoid exceptions on null/DBNull
            - Use Row[0] as the "current row" convention
        */

        private int GetIntFromSet(string tableName, string colName)
        {
            // Validate Data.Set and expected table exist
            if (Data.Set == null || !Data.Set.Tables.Contains(tableName))
                return 0;

            var t = Data.Set.Tables[tableName];

            // Validate a row exists and the column exists
            if (t.Rows.Count == 0 || !t.Columns.Contains(colName))
                return 0;

            object val = t.Rows[0][colName];
            if (val == null || val == DBNull.Value)
                return 0;

            int parsed;
            return int.TryParse(val.ToString(), out parsed) ? parsed : 0;
        }

        private void SetStringInSet(string tableName, string colName, string value)
        {
            // Validate Data.Set and expected table exist
            if (Data.Set == null || !Data.Set.Tables.Contains(tableName))
                return;

            var t = Data.Set.Tables[tableName];

            // Validate a row exists and the column exists
            if (t.Rows.Count == 0 || !t.Columns.Contains(colName))
                return;

            // Never write null into DataRow; keep it deterministic
            t.Rows[0][colName] = value ?? string.Empty;
        }

        // ============================================================
        // Rule metadata required by some P21 builds
        // ============================================================
        public override string GetName() => "C365_ScanPack_DeliveryInstructions";

        public override string GetDescription() =>
            "Reads oe_pick_ticket.instructions for the current pick ticket and writes it to ufc_scan_pack_ud_delivery_instructions in scan pack data entry.";
    }
}
