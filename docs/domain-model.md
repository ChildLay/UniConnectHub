# Domain model

Domain currently includes University, User, UserProfile, Skill, Interest and join records; connection requests, connections and blocks; communities/members; posts/comments/reactions/bookmarks; events/registrations; mentor profiles/requests; opportunities/saves; notifications; reports and audit logs. Entities enforce core local invariants such as no self connections or self mentoring, active-university registration, date ordering and valid state transitions. Feature application workflows are still being implemented.
