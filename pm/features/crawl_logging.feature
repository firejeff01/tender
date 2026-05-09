# language: zh-TW
Feature: 更新紀錄與錯誤日誌
  作為 標案搜尋工具的使用者與維運人員
  我希望 每次爬蟲執行的成功或失敗都被完整記錄，並能在介面上查看最近一次更新結果
  以便 在出現問題時可快速定位原因，並對「資料是否已更新」有可信證據

  Background:
    Given 今天日期為 "2026-05-08"
    And 系統已正確安裝並啟用每日 17:00 排程

  # ----------------------------------------------------------
  # 成功路徑
  # ----------------------------------------------------------
  Scenario: 每次成功執行皆寫入 crawl-runs.json
    Given "data/2026-05/2026-05-08/crawl-runs.json" 不存在
    When 每日 17:00 排程成功完成
    Then "data/2026-05/2026-05-08/crawl-runs.json" 應被建立
    And 該檔案應包含一筆 run 紀錄，欄位應包含 "runId"、"targetDate"、"startedAt"、"finishedAt"、"status"、"insertedCount"、"updatedCount"、"skippedCount"、"errorMessage"
    And 該筆 run 紀錄的 "status" 應為 "success"
    And 該筆 run 紀錄的 "errorMessage" 應為 null

  Scenario: 同日多次執行皆累積於 crawl-runs.json
    Given "data/2026-05/2026-05-08/crawl-runs.json" 已含 1 筆成功 run 紀錄
    When 使用者於同日按下「立即更新」並成功完成
    Then "crawl-runs.json" 的 "runs" 陣列應有 2 筆紀錄
    And 兩筆紀錄的 "runId" 應不相同
    And 兩筆紀錄的時間順序應為先前一筆在前、後一筆在後

  Scenario: 摘要應呈現當次新增/更新/略過筆數
    Given 政府電子採購網本次回傳 5 筆全新標案、2 筆更新標案、3 筆已存在標案
    When 爬蟲執行完成
    Then "crawl-runs.json" 最新一筆 run 紀錄應記錄 "insertedCount=5"、"updatedCount=2"、"skippedCount=3"
    And "summary.json" 應同步更新 "insertedCount"、"updatedCount"、"skippedCount" 與 "totalCount"

  # ----------------------------------------------------------
  # 失敗路徑
  # ----------------------------------------------------------
  Scenario: 整批失敗時寫入 failed run 紀錄與錯誤日誌
    Given 政府電子採購網所有重試均失敗
    When 排程觸發時間到達
    Then "crawl-runs.json" 應新增一筆 "status" 為 "failed" 的 run 紀錄
    And 該筆 run 紀錄的 "errorMessage" 應有錯誤摘要
    And "errors.log" 應寫入該次錯誤的詳細資訊

  Scenario: 部分頁面解析失敗仍保留錯誤紀錄
    Given 政府電子採購網查詢結果共 5 頁
    And 第 3 頁解析失敗
    When 爬蟲執行完成
    Then "errors.log" 應記錄第 3 頁的解析錯誤
    And "crawl-runs.json" 最新一筆 run 紀錄的 "status" 應為 "success"
    And "summary.json" 的 "errorMessage" 應提示「部分頁面解析失敗」

  Scenario: 檔案寫入失敗時記錄錯誤
    Given 本機磁碟空間不足
    When 爬蟲嘗試寫入 "tenders.json"
    Then "errors.log" 應記錄寫入失敗錯誤
    And "summary.json" 的 "lastRunStatus" 應為 "failed"

  # ----------------------------------------------------------
  # 介面呈現
  # ----------------------------------------------------------
  Scenario: 行事曆首頁側邊摘要顯示最近一次更新時間
    Given 系統最近一次成功爬蟲完成於 "2026-05-08 17:05:30"
    When 使用者開啟桌面程式
    Then 行事曆首頁側邊摘要應顯示「最近一次更新時間：2026-05-08 17:05:30」

  Scenario: 行事曆首頁側邊摘要顯示最近失敗紀錄
    Given "data/2026-05/2026-05-06/summary.json" 的 "lastRunStatus" 為 "failed"
    And 該日為近期最近一次失敗
    When 使用者開啟桌面程式
    Then 行事曆首頁側邊摘要應顯示「最近失敗紀錄」並指向 "2026-05-06"

  Scenario: 點擊行事曆失敗日的警示符號可查看錯誤摘要
    Given "data/2026-05/2026-05-06/summary.json" 的 "lastRunStatus" 為 "failed"
    And "data/2026-05/2026-05-06/summary.json" 的 "errorMessage" 為 "政府電子採購網連線逾時"
    When 使用者點擊 "2026-05-06" 的警示符號
    Then 系統應顯示錯誤摘要視窗，內容包含 "政府電子採購網連線逾時"
    And 視窗中應提供「查看完整錯誤日誌」連結指向當日 "errors.log"

  # ----------------------------------------------------------
  # 邊界情境
  # ----------------------------------------------------------
  Scenario: 同日資料夾尚未存在時，第一次寫入紀錄需自動建立
    Given "data/2026-05/2026-05-08/" 資料夾尚未存在
    When 爬蟲首次於該日成功完成
    Then 系統應自動建立 "data/2026-05/" 與 "data/2026-05/2026-05-08/" 資料夾
    And "crawl-runs.json"、"summary.json" 與 "tenders.json" 應同時存在於該資料夾

  Scenario: errors.log 隨單日累積，跨日不互相覆蓋
    Given "data/2026-05/2026-05-07/errors.log" 已存在並包含前日錯誤
    When 系統於 "2026-05-08" 寫入新的錯誤日誌
    Then "data/2026-05/2026-05-07/errors.log" 應維持原內容不被影響
    And 新錯誤應寫入 "data/2026-05/2026-05-08/errors.log"
