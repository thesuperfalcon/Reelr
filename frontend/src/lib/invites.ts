import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "./api";

export const INVITE_NOTE_MAX_LENGTH = 100;

export interface Invite {
  id: number;
  /** Who the invite is for. */
  note: string | null;
  createdAt: string;
  usedAt: string | null;
  /** The account that used it, if it still exists. */
  usedBy: string | null;
  revokedAt: string | null;
}

/** Returned once, when the invite is made: the code cannot be shown again. */
export interface CreatedInvite extends Invite {
  code: string;
  /** The sign-up page with the code filled in. */
  link: string;
}

const invitesKey = ["admin", "invites"];

export function useInvites() {
  return useQuery({
    queryKey: invitesKey,
    queryFn: () => api<Invite[]>("/api/admin/invites"),
  });
}

export function useCreateInvite() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (note: string) =>
      api<CreatedInvite>("/api/admin/invites", { method: "POST", body: JSON.stringify({ note: note || null }) }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: invitesKey }),
  });
}

export function useRevokeInvite() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (id: number) => api<void>(`/api/admin/invites/${id}`, { method: "DELETE" }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: invitesKey }),
  });
}
