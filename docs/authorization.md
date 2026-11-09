# Authorization

Policies are named centrally (`CanEditProfile`, `CanViewProfile`, `CanViewPost`, `CanComment`, `CanManageCommunity`, `CanManageEvent`, and `CanModerateContent`). The moderation policy requires an Admin or Moderator role. Resource-specific privacy and block enforcement must be applied by feature handlers when those endpoints are implemented. JWT role claims are supported, but there is no role-management workflow yet.
