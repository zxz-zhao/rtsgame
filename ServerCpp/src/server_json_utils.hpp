#pragma once

#include "server_types.hpp"

#include <nlohmann/json.hpp>

#include <string>

namespace ServerJson
{
using Json = nlohmann::json;

Json LoadJsonFile(const std::string& path);
bool SaveJsonFile(const std::string& path, const Json& jsonValue);

std::string TodayStringUtc();
std::int64_t CurrentTimeMs();

void EnsureUserDefaults(UserRecord& user);
void EnsureLobbyDay(UserRecord& user);
std::string RankTitleFrom(const UserRecord& user);

Json UserToSafeJson(const UserRecord& user);
Json LobbyTasksToJson(const UserRecord& user);
Json RoomToJson(const RoomRecord& room);

UserRecord UserFromJson(const Json& jsonValue);
Json UserToJson(const UserRecord& user);

RoomRecord RoomFromJson(const Json& jsonValue);
Json RoomToJsonStorage(const RoomRecord& room);

MatchQueueEntry MatchQueueEntryFromJson(const Json& jsonValue);
Json MatchQueueEntryToJson(const MatchQueueEntry& entry);

PendingMatch PendingMatchFromJson(const Json& jsonValue);
Json PendingMatchToJson(const PendingMatch& entry);

InviteRecord InviteFromJson(const Json& jsonValue);
Json InviteToJson(const InviteRecord& invite);

PaymentOrderRecord PaymentOrderFromJson(const Json& jsonValue);
Json PaymentOrderToJson(const PaymentOrderRecord& order);

BattleParticipantReport BattleParticipantReportFromJson(const Json& jsonValue);
Json BattleParticipantReportToJson(const BattleParticipantReport& report);

BattleSessionRecord BattleSessionFromJson(const Json& jsonValue);
Json BattleSessionToJson(const BattleSessionRecord& session);
}
