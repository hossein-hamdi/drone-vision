'use client';

import { getMeKeyGenerator, type GetMeResponse } from '@api/getMeQuery';
import { getProfileKeyGenerator, type GetProfileResponse } from '@api/getProfileQuery';
import {
  updateProfileKeyGenerator,
  type UpdateProfileRequestData,
  type UpdateProfileResponse,
} from '@api/updateProfileMutation';
import { useMutation, useQueryClient } from '@tanstack/react-query';

interface OptimisticContext {
  previousProfile?: GetProfileResponse;
  previousMe?: GetMeResponse;
}

export function useUpdateProfile() {
  const queryClient = useQueryClient();

  return useMutation<UpdateProfileResponse, Error, UpdateProfileRequestData, OptimisticContext>({
    mutationKey: updateProfileKeyGenerator(),
    onMutate: async ({ fullName, userName }) => {
      await Promise.all([
        queryClient.cancelQueries({ queryKey: getProfileKeyGenerator() }),
        queryClient.cancelQueries({ queryKey: getMeKeyGenerator() }),
      ]);

      const previousProfile = queryClient.getQueryData<GetProfileResponse>(getProfileKeyGenerator());
      const previousMe = queryClient.getQueryData<GetMeResponse>(getMeKeyGenerator());

      queryClient.setQueryData<GetProfileResponse>(getProfileKeyGenerator(), (prev) =>
        prev ? { ...prev, fullName, userName } : prev
      );
      queryClient.setQueryData<GetMeResponse>(getMeKeyGenerator(), (prev) =>
        prev ? { ...prev, fullName, userName } : prev
      );

      return { previousProfile, previousMe };
    },
    onError: (_err, _data, context) => {
      if (context?.previousProfile !== undefined) {
        queryClient.setQueryData(getProfileKeyGenerator(), context.previousProfile);
      }
      if (context?.previousMe !== undefined) {
        queryClient.setQueryData(getMeKeyGenerator(), context.previousMe);
      }
    },
    onSettled: () => {
      void queryClient.invalidateQueries({ queryKey: getProfileKeyGenerator() });
      void queryClient.invalidateQueries({ queryKey: getMeKeyGenerator() });
    },
  });
}
