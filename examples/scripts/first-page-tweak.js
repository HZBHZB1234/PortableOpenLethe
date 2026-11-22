/* {
  "id": "first-page-tweak",
  "version": "1.0.0",
  "kind": "script",
  "scope": "user",
  "permissions": ["log", "packet.modify"]
} */

// Requires an admin grant of packet.modify for this user+script.
// Rewrites only the executing user's own first-load response.
Hub.intercept.response("/api/LoadUserDataAll", function (res) {
  var r = res.result;
  if (r && r.profile) {
    r.profile.public_uid = "My Private Server";
    r.profile.level = 200;
  }
  if (r && r.danteAbilityList && r.danteAbilityList.length > 0)
    r.danteAbilityList[0].remaincount = 9999;
  if (res.updated && res.updated.userInfo)
    res.updated.userInfo.stamina = 99999;
  Log.info("first page rewritten");
  return res;
});
