/* {
  "id": "daily-checkin",
  "version": "1.0.0",
  "kind": "script",
  "scope": "user",
  "permissions": ["log", "account.read:self", "account.write:self", "endpoint"]
} */

// Daily check-in: on first account load each day, refill stamina and track a streak.
function today()    { return new Date().toISOString().slice(0, 10); }
function yesterday(){ return new Date(Date.now() - 86400000).toISOString().slice(0, 10); }
function load()     { return Account.get("CustomData") || {}; }

Hub.on("accountLoaded", function () {
  var d = load();
  if (d.lastCheckin === today()) return;
  var streak = d.lastCheckin === yesterday() ? (d.streak || 0) + 1 : 1;
  var info = Account.get("UserInfo") || {};
  info.stamina = 99999;
  Account.setField("UserInfo", info);
  Account.setField("CustomData", {
    lastCheckin: today(), streak: streak, total: (d.total || 0) + 1
  });
  Log.info("check-in day " + streak);
});

Hub.action("status", function () {
  var d = load();
  return { checkedInToday: d.lastCheckin === today(), streak: d.streak || 0, total: d.total || 0 };
});
