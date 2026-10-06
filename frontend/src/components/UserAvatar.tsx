import { Link } from "react-router";

interface UserAvatarProps {
  userName: string;
  imageUrl: string | null;
  /** Tailwind size and text classes, e.g. "size-12 text-xl". */
  className?: string;
}

// Profile picture, or the first letter of the username when there is none.
export function UserAvatar({ userName, imageUrl, className = "size-12 text-xl" }: UserAvatarProps) {
  if (imageUrl) {
    return <img src={imageUrl} alt="" loading="lazy" className={`shrink-0 rounded-full object-cover ${className}`} />;
  }

  return (
    <span
      className={`marquee flex shrink-0 items-center justify-center rounded-full bg-row text-haze ${className}`}
      aria-hidden="true"
    >
      {userName.charAt(0).toUpperCase()}
    </span>
  );
}

interface UserLinkProps {
  user: { id: number; userName: string; profileImageUrl: string | null };
  /** Text shown instead of the username, e.g. "Your review". */
  label?: string;
  avatarClassName?: string;
  className?: string;
}

// Avatar and name as one link to the user's page.
export function UserLink({ user, label, avatarClassName, className = "" }: UserLinkProps) {
  return (
    <Link to={`/user/${user.id}`} className={`group flex min-w-0 items-center gap-3 rounded-sm ${className}`}>
      <UserAvatar userName={user.userName} imageUrl={user.profileImageUrl} className={avatarClassName} />
      <span className="truncate font-medium group-hover:text-projector">{label ?? user.userName}</span>
    </Link>
  );
}
