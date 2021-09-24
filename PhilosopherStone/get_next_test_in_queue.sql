DROP PROCEDURE if EXISTS get_next_test_in_queue;

CREATE PROCEDURE get_next_test_in_queue()
BEGIN
    START TRANSACTION;
    select bsLogicInfoId, tradeLogicInfoId Into @bsId, @tlId FROM tradetests WHERE State = 0 ORDER BY PredictedPerformance Desc LIMIT 1 FOR UPDATE SKIP LOCKED;
    update tradeTests SET state = 1 WHERE bsLogicInfoId = @bsID and tradeLogicInfoId = @tlId;
    select * from tradetests WHERE bsLogicInfoId = @bsID and tradeLogicInfoId = @tlId;
     COMMIT;
END;